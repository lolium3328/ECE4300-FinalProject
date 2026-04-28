/// <summary>
/// 反馈控制器接口：表情/动作/BGM 三件事的统一抽象。
/// ProcessManager 通过该接口调用具体实现，便于后续替换：
///   - 默认实现 FeedbackController（辉夜：表情材质 + Animator + BGMManager）
///   - 后续可加 NullFeedbackController（关闭所有反馈用于测试）
///   - 后续可加其它角色专属实现
/// 7 个 Apply 方法对应 ProcessManager 的 7 个 state。
/// </summary>
public interface IFeedbackController
{
    /// <summary>Stage 1 开场对话/等待开始。</summary>
    void ApplyStage1_Intro();

    /// <summary>Stage 2 题目/准备。</summary>
    void ApplyStage2_Topic();

    /// <summary>Stage 3/4/5 做菜中（放松饼/果酱/topping）。</summary>
    void ApplyStage345_Cooking();

    /// <summary>Stage 6 结算：根据分数自行决定三档反馈。</summary>
    /// <param name="score">本轮分数（0-100）。</param>
    void ApplyStage6_Result(int score);

    /// <summary>Stage 6 进阶版：先播一遍 Pickup 动作（不循环），结束后再切到分数对应的结算反馈。
    /// 整个过程异步推进，调用方可通过 onResultBegin 回调在结算反馈生效那一刻同步触发对话等其它行为。</summary>
    /// <param name="score">本轮分数（0-100）。</param>
    /// <param name="onResultBegin">Pickup 动作播完、刚切到结算反馈时触发；可为 null。</param>
    void ApplyStage6_PickupThenResult(int score, System.Action onResultBegin);

    /// <summary>Stage 7 等待下一轮：根据上一轮分数决定反馈细节（低分会切到 Angry 等待）。</summary>
    /// <param name="lastScore">上一轮的分数（即刚结算完的那一轮）；初始进入时可传任意 ≥50 的值（如 60）。</param>
    void ApplyStage7_Wait(int lastScore);
}
