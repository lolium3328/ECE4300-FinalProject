using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class KaguyaCharacterController : MonoBehaviour
{
    [Serializable]
    public class ExpressionMaterialEntry
    {
        public string key;
        public Material material;
    }

    [Header("Core References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SkinnedMeshRenderer faceSkinnedRenderer;
    [SerializeField] private MeshRenderer faceMeshRenderer;
    [SerializeField] private int faceMaterialIndex = 0;

    [Header("Defaults")]
    [SerializeField] private string defaultAnimation = "Idle";
    [SerializeField] private string defaultExpression = "Default";

    [Header("Whitelists")]
    [SerializeField] private List<string> animationWhitelist = new List<string>
    {
        "Idle", "Walk", "Run", "Jump", "Attack", "Hit", "Death", "Skill", "Angry"
    };

    [SerializeField] private List<string> triggerWhitelist = new List<string>
    {
        "Action"
    };

    [Header("Expressions (Material Swap Only)")]
    [SerializeField] private List<ExpressionMaterialEntry> expressionMaterials = new List<ExpressionMaterialEntry>();

    [Header("Debug")]
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

    private void OnValidate()
    {
        if (faceMaterialIndex < 0)
        {
            faceMaterialIndex = 0;
        }

        CacheReferences();
        RebuildLookups();
    }

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

    public string[] GetAnimationNames()
    {
        return animationWhitelist.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    public string[] GetExpressionNames()
    {
        return expressionMaterials
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.key) && x.material != null)
            .Select(x => x.key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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
