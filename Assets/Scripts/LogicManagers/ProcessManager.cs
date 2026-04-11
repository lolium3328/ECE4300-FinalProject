using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcessManager : MonoBehaviour
{

    public static ProcessManager Instance { get; private set; }   //做成了全局静态实例，其他脚本通过ProcessManager.Instance直接访问

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
    }

    private void Update()   //流程管理
    {
        
    }


}
