using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcessManager : MonoBehaviour
{

    public static ProcessManager Instance { get; private set; }   //做成了全局静态实例，其他脚本通过ProcessManager.Instance直接访问
    [SerializeField] private DialogueData Dialogue1;    //开始的剧情对话
    [SerializeField] private DialogueData Dialogue2;    //结算对话，高分结局
    [SerializeField] private DialogueData Dialogue3;    //结算对话，中等分数结局
    [SerializeField] private DialogueData Dialogue4;    //结算对话，低分结局

    [SerializeField] private DialogueManager dialogueManager;    //对话管理器的引用，拖入DialogueManager脚本所在的对象
    [SerializeField] private CircleCountdown countdownTimer;    //倒计时UI组件的引用，拖入CircleCountdown脚本所在的对象
    [SerializeField] private UIManager uiManager;    //UI管理器的引用，拖入UIManager脚本所在的对象
    [SerializeField] private RecipeRoundController recipeRoundController;    //题目管理器的引用，拖入RecipeRoundController脚本所在的对象
    [SerializeField] private GestureSpawnSelector gestureSpawnSelector;    //手势放置控制器的引用，拖入HandSpawnController脚本所在的对象
    [SerializeField] private TriggerBoxJudge triggerBoxJudge;    //触发判定的引用，拖入TriggerBoxJudge脚本所在的对象
    [SerializeField] private CreamSurfacePlacementTester jamPlacementController;

    private int state = 1;
    /*
    ProcessManager负责管理做松饼的整个流程，这里定义了state状态：
    1：对话情节/等待开始
    2：题目/准备
    3：放置松饼
    4：放果酱
    5：放topping
    6：完成/结算对话
    7：结算分数/等待下一轮
    */
    private float timer = 0f;
    private int score = 60;    //分数，暂时没用到，后续可以根据制作的松饼质量来调整分数
    private int placeMode = 0;
    // 0: 禁用动作, 1: 放置(Spawn), 2: 手势/写(Gesture/Writing)


    public int State    //其他脚本通过ProcessManager.Instance.State访问当前状态，只读
    {
        get { return state; }
    }

    public int Score    //其他脚本通过ProcessManager.Instance.Score访问当前分数，读写
    {
        get { return score; }
        set { score = value; }
    }

    private void Awake()    //确保只有一个实例存在
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
        state = 1;    //初始状态为1，等待开始
        placeMode = 0;    //初始放置模式为0，禁用动作
        gestureSpawnSelector.ApplyRecognizedLabel("C");     //预设为空物体
        SetJamInputEnabled(false);

        // 启动完整对话序列
        dialogueManager.StartDialogue(Dialogue1, 
            () => { Debug.Log("对话完成！"); }
);
    }

    private void Update()   //流程管理，以及发信号
    {
        //给制作松饼计时
        if (state == 3 || state == 4 || state == 5)   //放置松饼、放果酱、放topping这三个状态需要计时
        {
            timer += Time.deltaTime;
        }
        else
        {
            timer = 0f;    //其他状态不计时，重置计时器
        }
        
    }

    public void SwitchToNextState()
    //流程管理里处理状态切换的全局接口，供其他脚本调用
    {
        state++;
        if (state > 7)
        {
            state = 2;    //循环回到状态2，开始下一轮
        }
        switch (state)
        {
            case 1:
                //这个状态不应该被切换函数切换到，保持在Start里
                gestureSpawnSelector.ApplyRecognizedLabel("C");     //预设为空物体          
                placeMode = 0;    //切回默认禁用状态
                SetJamInputEnabled(false);
                Debug.LogWarning("状态1不应该被切换函数切换到！");
                break;
            case 2:
                Debug.Log("switch to state 2");
                gestureSpawnSelector.ApplyRecognizedLabel("C");     //预设为空物体
                placeMode = 0;    //切回默认禁用状态
                SetJamInputEnabled(false);
                recipeRoundController.GenerateApplyAndJudge();   //生成本轮 recipe，并把同一份数据应用到 Judge
                uiManager.TriggerEndFinishStateUI();   //如果上一个状态是结算分数，先隐藏结算UI
                uiManager.TriggerReadyStateUI(recipeRoundController.CurrentRecipe);  //Ready UI 使用同一份 RuntimeJudgeRecipe 渲染菜单
                //动画放完后UIManager会调用ProcessManager.SwitchToNextState()来切换状态
                break;
            case 3:
                Debug.Log("switch to state 3");
                placeMode = 1;    //切换到放置状态
                SetJamInputEnabled(false);
                gestureSpawnSelector.ApplyRecognizedLabel("0");     //预设为松饼
                countdownTimer.StartCountdown(15f);   //激活倒计时动画
                StartCoroutine(WaitAndSwitch(15f, 3));    //放置松饼状态启动等待协程
                uiManager.TriggerPlacePancakeUI();   //激活放置松饼的UI提示
                break;
            case 4:
                Debug.Log("switch to state 4");
                placeMode = 2;    //果酱阶段先进入手势选择，选中 prefab 后再启用 jamPlacementController 放置
                SetJamInputEnabled(false);
                countdownTimer.StartCountdown(15f);   //激活倒计时动画
                StartCoroutine(WaitAndSwitch(15f, 4));    //放果酱状态启动等待协程
                uiManager.TriggerEndPlacePancakeUI();  //放置松饼的UI提示关闭
                uiManager.TriggerPlaceJamUI();   //激活放置果酱的UI提示
                break;
            case 5:
                gestureSpawnSelector.ApplyRecognizedLabel("C");     //预设为空物体
                SetJamInputEnabled(false);
                placeMode = 2;    //切换到手势/写字模式，等待玩家输入手势信号来选择topping
                uiManager.ChooseToppingHint();   //放置topping的UI提示
                //放置topping的ui在玩家输入手势信号后结束
                Debug.Log("switch to state 5");
                countdownTimer.StartCountdown(15f);   //激活倒计时动画
                StartCoroutine(WaitAndSwitch(15f, 5));    //放topping状态启动等待协程
                uiManager.TriggerEndPlaceJamUI();   //放置果酱的UI提示关闭
                uiManager.TriggerPlaceToppingUI();    //激活放置topping
                break;
            case 6:
                uiManager.EndChooseToppingHint();   //结束放置topping的UI提示,如果还没结束
                gestureSpawnSelector.ApplyRecognizedLabel("C");     //预设为空物体
                placeMode = 0;    //切回默认禁用状态
                SetJamInputEnabled(false);
                uiManager.TriggerEndPlaceToppingUI();    //激活结束放置topping
                Debug.Log("switch to state 6");

                triggerBoxJudge.JudgeNow();   //触发判定
                score = (int)triggerBoxJudge.LastTotalScore;    //获取分数,并转成int类型
                countdownTimer.StartCountdown(0f);   //如果上一个状态提前结束，主动隐藏倒计时UI
                if (score >= 80)   //根据分数调整结算对话，划分分数等级触发不一样的对话
                {
                    dialogueManager.StartDialogue(Dialogue2, 
                    () => { Debug.Log("结算对话完成！"); }
                    );
                }
                else if (score >= 50)
                {
                    dialogueManager.StartDialogue(Dialogue3, 
                    () => { Debug.Log("结算对话完成！"); }
                    );
                }
                else
                {
                    dialogueManager.StartDialogue(Dialogue4, 
                    () => { Debug.Log("结算对话完成！"); }
                    );
                }
                break;
            case 7:
                gestureSpawnSelector.ApplyRecognizedLabel("C");     //预设为空物体
                placeMode = 0;    //切回默认禁用状态
                SetJamInputEnabled(false);
                //UI显示分数
                uiManager.TriggerFinishStateUI();
                //可以打断当前结算对话，直接进入下一轮
                Debug.Log("switch to state 7");
                break;
        }
    }

    private IEnumerator WaitAndSwitch(float waitTime, int currentState)    //等待t秒的协程接口
    {
        yield return new WaitForSeconds(waitTime);
        if (state == currentState)     //如果在等待过程中状态已经切换了，就不执行切换了
        {
            SwitchToNextState();
        }   
    }

    public bool IsProhibitedMode()
    {
        return placeMode == 0;
    }

    public bool IsPlacementMode()
    {
        return placeMode == 1;
    }

    public bool IsGestureMode()
    {
        return placeMode == 2;
    }

    public void SetPlacementMode(int mode)
    {
        placeMode = mode;
    }

    private void SetJamInputEnabled(bool enabled)
    {
        if (jamPlacementController == null)
        {
            jamPlacementController = FindObjectOfType<CreamSurfacePlacementTester>(true);
        }

        if (jamPlacementController != null)
        {
            jamPlacementController.SetInputEnabled(enabled);
        }
    }

    public void SetPrefabToSpawnDone()      //收到信号预设完成时执行
    {
        if (state == 4)
        {
            placeMode = 1;    //果酱阶段手势选择完成后，切到放置模式并显示 cursor
            SetJamInputEnabled(true);
        }
        else if (state == 5)
        {
            placeMode = 1;    //只有 topping 阶段的手势选择会切到放置模式
            uiManager.EndChooseToppingHint();   //结束放置topping的UI提示,如果还没结束
        }
    }
}
