using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单行对话数据结构
/// 用来存储一句对话的所有信息：说话人、头像、文本内容
/// [System.Serializable] 让 Unity 能在 Inspector 中编辑这个类
/// </summary>
[System.Serializable]
public class DialogueLine
{
    public string speakerName;  // 说话人的名字（显示在对话框上方）
    public Sprite avatar;       // 说话人的头像图片
    [TextArea(2, 5)]            // 让 Inspector 中的输入框显示多行
    public string content;      // 对话的文本内容
}

/// <summary>
/// 对话数据资源文件（可以保存成 .asset 文件）
/// 用来存储一整个对话序列（多句对话）
/// [CreateAssetMenu] 让你能在 Project 窗口右键创建这个资源
/// 
/// 使用步骤：
/// 1. 在 Project 窗口右键 → Create → Dialogue → DialogueData
/// 2. 填入对话的句数（Lines 列表的大小）
/// 3. 编辑每一句对话的内容、说话人、头像
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/DialogueData")]
public class DialogueData : ScriptableObject
{
    public List<DialogueLine> lines;  // 对话列表，可以有多句
}

/// <summary>
/// 对话管理器 - 负责显示和控制游戏中的对话
/// 
/// 功能：
/// 1. 显示对话框 UI
/// 2. 逐字显示对话内容（打字机效果）
/// 3. 处理玩家输入（跳过/继续对话）
/// 4. 支持多句对话和单句对话
/// 
/// 使用方式：
/// // 方式1：使用对话数据资源
/// DialogueManager.Instance.StartDialogue(dialogueData, () => { Debug.Log("对话结束"); });
/// 
/// // 方式2：快速显示单句对话
/// DialogueManager.Instance.ShowSingleLine("NPC", "你好，让我们开始冒险吧！", null);
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;  // 单例模式，全局访问唯一的 DialogueManager

    [Header("UI引用")]  // 在 Inspector 中的分组标题
    [SerializeField] private GameObject dialoguePanel;      // 整个对话框背景面板
    [SerializeField] private Image avatarImage;             // 说话人的头像图片
    [SerializeField] private TextMeshProUGUI nameText;      // 显示说话人名字的文本
    [SerializeField] private TextMeshProUGUI contentText;   // 显示对话内容的文本
    [SerializeField] private GameObject continueArrow;      // "继续" 箭头提示（脉冲效果用）

    [Header("设置")]  // 设置项分组
    [SerializeField] private float typingSpeed = 0.05f;  // 打字速度，每个字显示的时间（秒）

    // ========== 内部状态变量 ==========
    private Queue<DialogueLine> lineQueue = new Queue<DialogueLine>();  // 对话队列，存储待显示的对话
    private string currentFullText;          // 当前要显示的完整文本内容
    private bool isTyping = false;           // 是否正在打字中
    private bool isActive = false;           // 对话框是否处于活动状态
    private Coroutine typingCoroutine;       // 打字协程的引用（用来停止和重启）
    private System.Action onEnd;             // 对话结束时的回调函数

    private void Awake()    // 单例模式实现，确保只有一个 DialogueManager 实例存在
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {

    }

    private void Update()
    {

    }

    public void TriggerNextInput()   //外部调用的接口，触发下一句对话
    {
        if (!isActive) return;

        if (isTyping)
        {
            CompleteTyping();
        }
        else
        {
            ShowNextLine();
        }
    }

    // ============ 公开接口（其他脚本调用的方法）============

    /// <summary>
    /// 启动完整的对话序列（使用对话数据资源）
    /// 
    /// 参数说明：
    /// - data: 你创建的对话数据资源（.asset 文件）
    /// - onEnd: 对话结束时的回调函数（可选）
    /// 
    /// 使用例子：
    ///   // 在某个 NPC 脚本中
    ///   DialogueManager.Instance.StartDialogue(myDialogueData, () => {
    ///       Debug.Log("对话完成！");
    ///       // 在这里可以触发后续逻辑，比如任务进度、剧情推进等
    ///   });
    /// </summary>
    public void StartDialogue(DialogueData data, System.Action onEnd = null)
    {
        // 保存结束时的回调
        this.onEnd = onEnd;
        
        // 清空队列，准备放入新的对话
        lineQueue.Clear();
        
        // 重置状态变量（重要！确保第二次调用时状态正确）
        isTyping = false;
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 把数据中的所有对话行加入队列
        foreach (var line in data.lines)
            lineQueue.Enqueue(line);

        // 激活对话框
        isActive = true;
        dialoguePanel.SetActive(true);      // 显示对话框
        continueArrow.SetActive(false);     // 隐藏继续箭头（打字中不显示）

        // 显示第一句对话
        ShowNextLine();
    }

    /// <summary>
    /// 快速显示单句对话（不需要创建资源文件，适合临时对话）
    /// 
    /// 参数说明：
    /// - speaker: 说话人的名字
    /// - content: 对话内容
    /// - avatar: 说话人的头像（可选，null 表示不显示头像）
    /// - onEnd: 对话结束时的回调（可选）
    /// 
    /// 使用例子：
    ///   DialogueManager.Instance.ShowSingleLine(
    ///       "系统",
    ///       "欢迎来到游戏！",
    ///       null,  // 不显示头像
    ///       () => { Debug.Log("系统提示结束"); }
    ///   );
    /// </summary>
    public void ShowSingleLine(string speaker, string content, 
                               Sprite avatar = null, System.Action onEnd = null)
    {
        // 保存结束回调
        this.onEnd = onEnd;
        
        // 清空队列
        lineQueue.Clear();
        
        // 重置状态变量（重要！确保第二次调用时状态正确）
        isTyping = false;
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        // 创建一个临时的对话行并加入队列
        lineQueue.Enqueue(new DialogueLine
        {
            speakerName = speaker,
            content = content,
            avatar = avatar
        });

        // 激活对话框
        isActive = true;
        dialoguePanel.SetActive(true);
        continueArrow.SetActive(false);

        // 显示这句对话
        ShowNextLine();
    }

    // ============ 内部逻辑方法（不需要外部调用）============

    /// <summary>
    /// 显示下一句对话
    /// 如果队列为空，则结束对话
    /// 否则取出队列第一个对话，更新 UI，启动打字动画
    /// </summary>
    void ShowNextLine()
    {
        // 检查队列是否还有对话
        if (lineQueue.Count == 0)
        {
            // 没有对话了，结束对话框
            EndDialogue();
            return;
        }

        // 从队列取出第一句对话
        DialogueLine line = lineQueue.Dequeue();

        // 更新说话人名字
        nameText.text = line.speakerName;

        // 更新头像（如果有的话）
        if (line.avatar != null)
        {
            avatarImage.sprite = line.avatar;           // 设置新头像
            avatarImage.gameObject.SetActive(true);     // 显示头像
        }
        else
        {
            avatarImage.gameObject.SetActive(false);    // 隐藏头像
        }

        // 保存对话文本（完整版本）
        currentFullText = line.content;

        // 停止可能还在进行的打字协程（防止重复启动）
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        // 启动新的打字协程
        typingCoroutine = StartCoroutine(TypeText());
    }

    /// <summary>
    /// 打字动画协程
    /// 一个字一个字显示文本，产生打字机效果
    /// </summary>
    IEnumerator TypeText()
    {
        isTyping = true;                   // 标记正在打字
        continueArrow.SetActive(false);    // 隐藏继续箭头
        contentText.text = "";             // 清空文本框

        // 逐个字符遍历对话内容
        foreach (char c in currentFullText)
        {
            // 每次添加一个字符
            contentText.text += c;

            // TODO: 可以在这里播放打字音效（取消注释当你有音效管理器时）
            // AudioManager.Instance.PlayTypingSound();

            // 等待一段时间后再显示下一个字符
            yield return new WaitForSeconds(typingSpeed);
        }

        // 打字完成，调用完成处理
        FinishTyping();
    }

    /// <summary>
    /// 立刻完成打字（跳过剩余动画）
    /// 玩家按键时，如果还在打字就会调用这个方法
    /// </summary>
    void CompleteTyping()
    {
        // 停止打字协程
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        // 直接显示全部文本
        contentText.text = currentFullText;
        
        // 标记打字完成
        FinishTyping();
    }

    /// <summary>
    /// 打字完成后的处理
    /// 显示"继续"箭头，标记打字状态为完成
    /// </summary>
    void FinishTyping()
    {
        isTyping = false;                    // 标记打字已完成
        continueArrow.SetActive(true);       // 显示继续箭头提示玩家
    }

    /// <summary>
    /// 结束对话，隐藏对话框，执行回调
    /// </summary>
    void EndDialogue()
    {
        isActive = false;                  // 标记对话框不再活动
        dialoguePanel.SetActive(false);    // 隐藏对话框
        onEnd?.Invoke();                   // 执行回调函数（如果设置了的话）

        ProcessManager.Instance.SwitchToNextState();  // 对话结束后通知ProcessManager切换到下一个状态
    }
}

/*
╔══════════════════════════════════════════════════════════════════════════════╗
║                          使用步骤总结                                        ║
╠══════════════════════════════════════════════════════════════════════════════╣
║                                                                              ║
║ 【第一步】在 Unity 编辑器中设置 DialogueManager：                          ║
║   1. 创建一个空的 GameObject，命名为 "DialogueManager"                      ║
║   2. 把这个脚本 (DialogueManager.cs) 挂到这个物体上                         ║
║   3. 在 Hierarchy 中创建对话 UI 面板，包含：                               ║
║      - 背景面板 (Panel)                                                    ║
║      - 头像图片 (Image)                                                    ║
║      - 说话人名字文本 (Text)                                               ║
║      - 对话内容文本 (Text) ← 必须用 TextMeshPro！                         ║
║      - 继续箭头 (GameObject，可以是图片、UI 按钮等)                       ║
║   4. 在 Inspector 中把这些 UI 元素拖到对应的字段                           ║
║   5. 调整 typingSpeed（打字速度）                                          ║
║                                                                              ║
║ 【第二步】创建对话数据资源：                                               ║
║   1. 在 Project 窗口右键 → Create → Dialogue → DialogueData                ║
║   2. 名字可以叫 "MyDialogue1"、"NPCGreeting" 等                           ║
║   3. 选中这个资源，在 Inspector 中：                                       ║
║      - 设置 Lines 的数量（有几句对话）                                    ║
║      - 填入每句的说话人、文本、头像（头像可选）                           ║
║                                                                              ║
║ 【第三步】在脚本中使用对话管理器：                                         ║
║                                                                              ║
║   // 方式1：用对话资源启动完整序列                                         ║
║   public class NPCController : MonoBehaviour                                ║
║   {                                                                          ║
║       public DialogueData greetingDialogue;  // 在 Inspector 中拖入资源    ║
║                                                                              ║
║       public void OnPlayerApproach()                                        ║
║       {                                                                      ║
║           DialogueManager.Instance.StartDialogue(                          ║
║               greetingDialogue,                                             ║
║               () => {                                                       ║
║                   Debug.Log("对话完成，可以触发剧情");                      ║
║                   // 你的后续逻辑...                                        ║
║               }
║           );                                                                 ║
║       }                                                                      ║
║   }                                                                          ║
║                                                                              ║
║   // 方式2：快速显示单句对话                                               ║
║   DialogueManager.Instance.ShowSingleLine(                                 ║
║       "系统",                                                               ║
║       "欢迎来到我的店铺！",                                                 ║
║       null,  // 不显示头像                                                 ║
║       () => { /* 对话完成后的逻辑 }                               
║ 【第四步】玩家交互：                                                        ║
║   - 按 Space 键或鼠标左键                                                  ║
║   - 如果还在打字：跳过动画，立刻显示全部                                   ║
║   - 如果已显示完：显示下一句对话                                           ║
║   - 对话全部完成：隐藏对话框，执行回调    
*/