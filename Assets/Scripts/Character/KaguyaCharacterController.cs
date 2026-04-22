using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 角色动作与表情控制脚本。
/// 负责：播放动画状态、切换面部表情材质、提供外部可调用接口。
/// </summary>
[DisallowMultipleComponent]
public class KaguyaCharacterController : MonoBehaviour
{
    [Serializable]
    public class ExpressionMaterialEntry
    {
        /// <summary>表情键名（如 Default / Happy / Angry）。</summary>
        public string key;
        /// <summary>对应表情使用的材质。</summary>
        public Material material;
    }

    [Header("Core References")]
    // 角色 Animator 组件。
    [SerializeField] private Animator animator;
    // 面部使用蒙皮网格时的渲染器。
    [SerializeField] private SkinnedMeshRenderer faceSkinnedRenderer;
    // 面部使用普通网格时的渲染器（兜底）。
    [SerializeField] private MeshRenderer faceMeshRenderer;
    // 需要替换的面部材质槽位索引。
    [SerializeField] private int faceMaterialIndex = 0;

    [Header("Defaults")]
    // 默认播放的动画状态名。
    [SerializeField] private string defaultAnimation = "Armature|Happy";
    // 默认表情键。
    [SerializeField] private string defaultExpression = "Happy";

    [Header("Whitelists")]
    // 允许外部调用的动画状态白名单。
    [SerializeField] private List<string> animationWhitelist = new List<string>
    {
        "Armature|Angry", "Armature|Cheer", "Armature|Excited", "Armature|Happy", "Armature|mixamo_com", "Armature|Pickup", "Armature|Sad", "Armature|Tpose", "Armature|Walk"
    };

    // 允许外部触发的 Trigger 参数白名单。
    [SerializeField] private List<string> triggerWhitelist = new List<string>
    {
        "Action"
    };

    [Header("Expressions (Material Swap Only)")]
    // 表情键与材质映射配置列表。
    [SerializeField] private List<ExpressionMaterialEntry> expressionMaterials = new List<ExpressionMaterialEntry>();

    [Header("Debug")]
    // 是否输出警告日志。
    [SerializeField] private bool logWarnings = true;

    private readonly Dictionary<string, Material> expressionMap = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> animationSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> triggerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        CacheReferences();
        RebuildLookups();
        ResetToDefaultState();
    }

    private void Update()
    {
        // 动作按键绑定 (Z, X, C, V, B, N, M, K, L)
        if (Input.GetKeyDown(KeyCode.Z)) PlayAnimation("Armature|Angry");
        if (Input.GetKeyDown(KeyCode.X)) PlayAnimation("Armature|Cheer");
        if (Input.GetKeyDown(KeyCode.C)) PlayAnimation("Armature|Excited");
        if (Input.GetKeyDown(KeyCode.V)) PlayAnimation("Armature|Happy");
        if (Input.GetKeyDown(KeyCode.B)) PlayAnimation("Armature|mixamo_com");
        if (Input.GetKeyDown(KeyCode.N)) PlayAnimation("Armature|Pickup");
        if (Input.GetKeyDown(KeyCode.M)) PlayAnimation("Armature|Sad");
        if (Input.GetKeyDown(KeyCode.K)) PlayAnimation("Armature|Tpose");
        if (Input.GetKeyDown(KeyCode.L)) PlayAnimation("Armature|Walk");

        // 表情按键绑定 (u, i, o, p)
        if (Input.GetKeyDown(KeyCode.U)) SetExpression("Angry");
        if (Input.GetKeyDown(KeyCode.I)) SetExpression("Happy");
        if (Input.GetKeyDown(KeyCode.O)) SetExpression("Sad");
        if (Input.GetKeyDown(KeyCode.P)) SetExpression("SuperHappy");
    }

    private void OnValidate()
    {
        if (faceMaterialIndex < 0)
        {
            faceMaterialIndex = 0;
        }

        CacheReferences();
        RebuildLookups();
    }

    /// <summary>
    /// 播放指定动作状态名。
    /// stateName 需要与 Animator 中的状态名一致。
    /// </summary>
    public bool PlayAnimation(string stateName, float transition = 0.1f)
    {
        if (!EnsureAnimator())
        {
            return false;
        }

        var requested = Normalize(stateName);
        if (string.IsNullOrEmpty(requested))
        {
            Warn("PlayAnimation received empty state name. Falling back to default animation."
            );
            requested = defaultAnimation;
        }

        var resolved = ResolveAnimationName(requested);
        if (!TryCrossFade(resolved, transition))
        {
            if (!string.Equals(resolved, defaultAnimation, StringComparison.OrdinalIgnoreCase) && TryCrossFade(defaultAnimation, transition))
            {
                Warn("State '" + requested + "' is unavailable. Fell back to default animation '" + defaultAnimation + "'.");
                return true;
            }

            Warn("Unable to play animation state '" + requested + "'.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 按白名单索引播放动作。
    /// 越界时会回退到默认动作。
    /// </summary>
    public bool PlayAnimationByIndex(int index, float transition = 0.1f)
    {
        if (index < 0 || index >= animationWhitelist.Count)
        {
            Warn("PlayAnimationByIndex out of range: " + index + ". Falling back to default animation."
            );
            return PlayAnimation(defaultAnimation, transition);
        }

        return PlayAnimation(animationWhitelist[index], transition);
    }

    /// <summary>
    /// 按表情键切换面部材质。
    /// 例如：Default / Happy / Sad / Angry。
    /// </summary>
    public bool SetExpression(string expressionKey)
    {
        var requested = Normalize(expressionKey);
        if (string.IsNullOrEmpty(requested))
        {
            Warn("SetExpression received empty key. Falling back to default expression."
            );
            requested = defaultExpression;
        }

        Material target;
        if (!expressionMap.TryGetValue(requested, out target) || target == null)
        {
            Warn("Expression key '" + requested + "' is not in whitelist/map. Falling back to default expression '" + defaultExpression + "'.");
            if (!expressionMap.TryGetValue(defaultExpression, out target) || target == null)
            {
                Warn("Default expression material is missing. Expression switch skipped."
                );
                return false;
            }
        }

        return ApplyFaceMaterialSlot0(target);
    }

    /// <summary>
    /// 按表情列表索引切换。
    /// 越界时回退到默认表情。
    /// </summary>
    public bool SetExpressionByIndex(int index)
    {
        if (index < 0 || index >= expressionMaterials.Count)
        {
            Warn("SetExpressionByIndex out of range: " + index + ". Falling back to default expression."
            );
            return SetExpression(defaultExpression);
        }

        var key = expressionMaterials[index] != null ? expressionMaterials[index].key : null;
        return SetExpression(key);
    }

    /// <summary>
    /// 设置动画速度参数 Speed。
    /// </summary>
    public void SetSpeed(float value)
    {
        if (!EnsureAnimator())
        {
            return;
        }

        if (!HasParameter("Speed", AnimatorControllerParameterType.Float))
        {
            Warn("Animator float parameter 'Speed' not found."
            );
            return;
        }

        animator.SetFloat("Speed", value);
    }

    /// <summary>
    /// 设置移动状态参数 IsMoving。
    /// </summary>
    public void SetMoving(bool value)
    {
        if (!EnsureAnimator())
        {
            return;
        }

        if (!HasParameter("IsMoving", AnimatorControllerParameterType.Bool))
        {
            Warn("Animator bool parameter 'IsMoving' not found."
            );
            return;
        }

        animator.SetBool("IsMoving", value);
    }

    /// <summary>
    /// 触发 Animator 的 Trigger 参数。
    /// 未命中白名单时会回退到 Action。
    /// </summary>
    public void TriggerAction(string triggerName)
    {
        if (!EnsureAnimator())
        {
            return;
        }

        var requested = Normalize(triggerName);
        if (string.IsNullOrEmpty(requested) || !triggerSet.Contains(requested))
        {
            Warn("Trigger '" + requested + "' is not whitelisted. Falling back to 'Action'.");
            requested = "Action";
        }

        if (!HasParameter(requested, AnimatorControllerParameterType.Trigger))
        {
            Warn("Animator trigger parameter '" + requested + "' not found."
            );
            return;
        }

        animator.ResetTrigger(requested);
        animator.SetTrigger(requested);
    }

    /// <summary>
    /// 获取当前可配置的动作名称白名单。
    /// </summary>
    public string[] GetAnimationNames()
    {
        return animationWhitelist.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    /// <summary>
    /// 获取当前可配置的表情键列表。
    /// </summary>
    public string[] GetExpressionNames()
    {
        return expressionMaterials
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.key) && x.material != null)
            .Select(x => x.key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 重置到默认动作与默认表情。
    /// 同时将 Speed 和 IsMoving 复位。
    /// </summary>
    public void ResetToDefaultState()
    {
        PlayAnimation(defaultAnimation, 0f);
        SetSpeed(0f);
        SetMoving(false);
        SetExpression(defaultExpression);
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (faceSkinnedRenderer == null && faceMeshRenderer == null)
        {
            var face = FindFaceTransform();
            if (face != null)
            {
                faceSkinnedRenderer = face.GetComponent<SkinnedMeshRenderer>();
                if (faceSkinnedRenderer == null)
                {
                    faceMeshRenderer = face.GetComponent<MeshRenderer>();
                }
            }
        }
    }

    private Transform FindFaceTransform()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(t.name, "Face", StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private void RebuildLookups()
    {
        animationSet.Clear();
        foreach (var item in animationWhitelist)
        {
            var normalized = Normalize(item);
            if (!string.IsNullOrEmpty(normalized))
            {
                animationSet.Add(normalized);
            }
        }

        if (!string.IsNullOrWhiteSpace(defaultAnimation))
        {
            animationSet.Add(defaultAnimation.Trim());
        }

        triggerSet.Clear();
        foreach (var item in triggerWhitelist)
        {
            var normalized = Normalize(item);
            if (!string.IsNullOrEmpty(normalized))
            {
                triggerSet.Add(normalized);
            }
        }

        if (!triggerSet.Contains("Action"))
        {
            triggerSet.Add("Action");
        }

        expressionMap.Clear();
        foreach (var entry in expressionMaterials)
        {
            if (entry == null)
            {
                continue;
            }

            var normalized = Normalize(entry.key);
            if (string.IsNullOrEmpty(normalized) || entry.material == null)
            {
                continue;
            }

            expressionMap[normalized] = entry.material;
        }
    }

    private bool EnsureAnimator()
    {
        if (animator != null)
        {
            return true;
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Warn("Animator reference is missing on character root."
            );
            return false;
        }

        return true;
    }

    private string ResolveAnimationName(string requested)
    {
        if (animationSet.Contains(requested))
        {
            return requested;
        }

        Warn("Animation '" + requested + "' is not whitelisted. Falling back to default animation '" + defaultAnimation + "'.");
        return defaultAnimation;
    }

    private bool TryCrossFade(string stateName, float transition)
    {
        var normalized = Normalize(stateName);
        if (string.IsNullOrEmpty(normalized) || animator == null)
        {
            return false;
        }

        var stateHash = Animator.StringToHash(normalized);
        if (!animator.HasState(0, stateHash))
        {
            return false;
        }

        animator.CrossFade(stateHash, Mathf.Max(0f, transition), 0);
        return true;
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (var p in animator.parameters)
        {
            if (p.type == parameterType && string.Equals(p.name, parameterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool ApplyFaceMaterialSlot0(Material material)
    {
        if (material == null)
        {
            Warn("ApplyFaceMaterialSlot0 received null material."
            );
            return false;
        }

        if (faceSkinnedRenderer != null)
        {
            var mats = faceSkinnedRenderer.sharedMaterials ?? new Material[0];
            if (mats.Length <= faceMaterialIndex)
            {
                Array.Resize(ref mats, faceMaterialIndex + 1);
            }

            mats[faceMaterialIndex] = material;
            faceSkinnedRenderer.sharedMaterials = mats;
            return true;
        }

        if (faceMeshRenderer != null)
        {
            var mats = faceMeshRenderer.sharedMaterials ?? new Material[0];
            if (mats.Length <= faceMaterialIndex)
            {
                Array.Resize(ref mats, faceMaterialIndex + 1);
            }

            mats[faceMaterialIndex] = material;
            faceMeshRenderer.sharedMaterials = mats;
            Warn("Face SkinnedMeshRenderer not found. Applied material on Face MeshRenderer slot " + faceMaterialIndex + " as fallback."
            );
            return true;
        }

        Warn("Face renderer references are missing. Assign Face SkinnedMeshRenderer or MeshRenderer."
        );
        return false;
    }

    private string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private void Warn(string message)
    {
        if (logWarnings)
        {
            Debug.LogWarning("[KaguyaCharacterController] " + message, this);
        }
    }
}
