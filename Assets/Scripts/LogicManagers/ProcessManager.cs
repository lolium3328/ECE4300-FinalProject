using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcessManager : MonoBehaviour
{

    public static ProcessManager Instance { get; private set; }   //做成了全局静态实例，其他脚本通过ProcessManager.Instance直接访问
    [SerializeField] private DialogueData myDialogueData;    //对话数据，后续可以在编辑器里设置成public，或者通过其他方式加载，这里先不写了

    private int state = 1;
    /*
    ProcessManager负责管理做松饼的整个流程，这里定义了state状态：
    1：情节/等待开始
    2：题目/准备
    3：放置松饼
    4：放果酱
    5：放topping
    6：完成/结算
    7：等待下一轮
    */
    private float timer = 0f;

    public int State    //其他脚本通过ProcessManager.Instance.State访问当前状态，只读
    {
        get { return state; }
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
        // 启动完整对话序列
        DialogueManager.Instance.StartDialogue(myDialogueData, 
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

        //根据state的7种值执行不同流程逻辑
        switch (state)
        {
            case 1:
                //情节/等待开始
                Debug.Log("In state 1");
                break;
            case 2:
                //UI出示题目，倒数准备开始
                //过一段时间自动进入下一个状态，开始制作松饼，已写在协程里了
                Debug.Log("In state 2");
                break;
            case 3:
                //5秒钟放置松饼，超过5秒自动切换到下一状态,已写在协程里了
                Debug.Log("In state 3");
                break;
            case 4:
                //5秒钟放果酱，超过5秒自动切换到下一状态,已写在协程里了
                Debug.Log("In state 4");
                break;
            case 5:
                //5秒钟放topping，超过5秒自动切换到下一状态,已写在协程里了
                Debug.Log("In state 5");
                break;
            case 6:
                //完成/结算
                Debug.Log("In state 6");
                break;
            case 7:
                //等待下一轮
                Debug.Log("In state 7");
                break;
            default:
                state = 2;
                break;
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
            case 2:
                //UI出示题目，倒数准备开始，放完自动进下一个状态
                StartCoroutine(WaitForState2AndSwitch(8f));     //具体等待需要调整，这里假设是8秒
                break;
            case 3:
                StartCoroutine(WaitForState3AndSwitch(5f));    //放置松饼状态启动等待协程
                break;
            case 4:
                StartCoroutine(WaitForState4AndSwitch(5f));    //放果酱状态启动等待协程
                break;
            case 5:
                StartCoroutine(WaitForState5AndSwitch(5f));    //放topping状态启动等待协程
                break;
        }
    }

    private IEnumerator WaitForState2AndSwitch(float waitTime=8f)    //准备状态等待5秒的协程接口
    {
        yield return new WaitForSeconds(waitTime);
        if (state == 2)     //如果在等待过程中状态已经切换了，就不执行切换了
        {
            SwitchToNextState();
        }
    }

    private IEnumerator WaitForState3AndSwitch(float waitTime=5f)    //放置松饼等待5秒的协程接口
    {
        yield return new WaitForSeconds(waitTime);
        if (state == 3)     //如果在等待过程中状态已经切换了，就不执行切换了
        {
            SwitchToNextState();
        }   
    }

    private IEnumerator WaitForState4AndSwitch(float waitTime=5f)    //放果酱等待5秒的协程接口
    {
        yield return new WaitForSeconds(waitTime);
        if (state == 4)     //如果在等待过程中状态已经切换了，就不执行切换了
        {
            SwitchToNextState();
        }   
    }

    private IEnumerator WaitForState5AndSwitch(float waitTime=5f)    //放topping等待5秒的协程接口
    {
        yield return new WaitForSeconds(waitTime);
        if (state == 5)     //如果在等待过程中状态已经切换了，就不执行切换了
        {
            SwitchToNextState();
        }   
    }

}
