using UnityEngine;

/// <summary>
/// 集中管理角色表情/动作/BGM 三者的反馈控制器。
/// 由 ProcessManager 在每个 state 切换时调用对应的 ApplyStageX_xxx() 方法。
/// 表情：通过切换 Face GO 的 MeshRenderer.material 实现。
/// 动作：通过 Animator.CrossFade(stateName) 直接按 state 全名切换，不依赖 Trigger 参数。
/// BGM：调用现有 BGMManager.Instance 的封装接口。
/// </summary>
public class FeedbackController : MonoBehaviour, IFeedbackController
{
    /// <summary>全局单例，跨场景访问入口。可能为 null（当前场景未挂载实例时）。</summary>
    public static IFeedbackController Instance { get; private set; }

    [Tooltip("勾选后，加载新场景时本对象不被销毁。仅在该实例位于会被切走的场景时需要勾。")]
    [SerializeField] private bool dontDestroyOnLoad = false;

    private void Awake()
    {
        if (Instance != null && !object.ReferenceEquals(Instance, this))
        {
            Debug.LogWarning("[FeedbackController] 已存在另一实例，本实例将被销毁: " + name);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (object.ReferenceEquals(Instance, this)) Instance = null;
    }

    [Header("Face Renderer (Face GO 的 MeshRenderer)")]
    [SerializeField] private MeshRenderer faceRenderer;

    [Header("Expression Materials")]
    [SerializeField] private Material matDefault;
    [SerializeField] private Material matHappy;
    [SerializeField] private Material matSuperHappy;
    [SerializeField] private Material matSad;
    [SerializeField] private Material matAngry;

    [Header("Animator (角色根上的 Animator)")]
    [SerializeField] private Animator characterAnimator;

    [Header("Animator State 全名 (与 Kaguya.controller 中的 state 名一致)")]
    [SerializeField] private string stateExcited = "Armature|Excited";
    [SerializeField] private string stateCheer   = "Armature|Cheer";
    [SerializeField] private string stateHappy   = "Armature|Happy";
    [SerializeField] private string stateSad     = "Armature|Sad";
    [SerializeField] private string statePickup  = "Armature|Pickup";
    [SerializeField] private string stateWalk    = "Armature|Walk";

    [Header("CrossFade 时长 (秒)")]
    [SerializeField] private float crossfadeDuration = 0.15f;

    // Stage 1 / 3 / 4 / 5 / 7 默认状态：Happy 表情 + Happy 动作
    public void ApplyStage1_Intro()
    {
        SetExpr(matHappy);
        PlayAnim(stateHappy);
        if (BGMManager.Instance != null) BGMManager.Instance.PlaySoftBGM2();
    }

    // Stage 2 题目/准备：Happy 表情 + Pickup 动作 + 轻快做菜 BGM
    public void ApplyStage2_Topic()
    {
        SetExpr(matHappy);
        PlayAnim(statePickup);
        if (BGMManager.Instance != null) BGMManager.Instance.PlayUpbeatBGM1();
    }

    // Stage 3/4/5 做菜中：Happy 表情 + Happy 动作；BGM 不切
    public void ApplyStage345_Cooking()
    {
        SetExpr(matHappy);
        PlayAnim(stateHappy);
    }

    // Stage 6 结算：按分数三档分发表情/动作/BGM
    public void ApplyStage6_Result(int score)
    {
        if (score >= 80)
        {
            SetExpr(matSuperHappy);
            PlayAnim(stateExcited);
            if (BGMManager.Instance != null) BGMManager.Instance.PlayHighScoreBGM1();
        }
        else if (score >= 50)
        {
            SetExpr(matHappy);
            PlayAnim(stateCheer);
            if (BGMManager.Instance != null) BGMManager.Instance.PlaySettlementBGM();
        }
        else
        {
            SetExpr(matSad);
            PlayAnim(stateSad);
            if (BGMManager.Instance != null) BGMManager.Instance.PlayLowScoreFailBGM();
        }
    }

    // Stage 7 等待下一轮：Happy 表情 + Happy 动作 + 收尾 BGM
    public void ApplyStage7_Wait()
    {
        SetExpr(matHappy);
        PlayAnim(stateHappy);
        if (BGMManager.Instance != null) BGMManager.Instance.PlayEndingBGM();
    }

    private void SetExpr(Material m)
    {
        if (faceRenderer != null && m != null) faceRenderer.sharedMaterial = m;
    }

    private void PlayAnim(string stateName)
    {
        if (characterAnimator != null && !string.IsNullOrEmpty(stateName))
            characterAnimator.CrossFadeInFixedTime(stateName, crossfadeDuration);
    }
}
