using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }   //做成了全局静态实例，其他脚本通过InputManager.Instance直接访问
    [SerializeField] private DialogueManager dialogueManager;    //对话管理器的引用，拖入DialogueManager脚本所在的对象
    [SerializeField] private HandSpawnController handSpawnController;    //手势放置控制器的引用，拖入HandSpawnController脚本所在的对象
    [SerializeField] private GestureSpawnSelector gestureSpawnSelector;    //手势预设选择器的引用，拖入GestureSpawnSelector脚本所在的对象

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

    private void Update()   //监听输入
    {
        if (Input.GetKey(KeyCode.Escape))   //Esc返回主菜单或退出游戏，我把这个切场景顺便放进了InputManager里了
        {
            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                Application.Quit();
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        //监听手势输入，这里以空格为例测试输入系统
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (ProcessManager.Instance.State == 1 || ProcessManager.Instance.State == 6)   //如果当前状态是1，按空格过对话情节
            {
                dialogueManager.TriggerNextInput();
            }
            else if (ProcessManager.Instance.State == 2)
            {
                
            }
            else
            {
                ProcessManager.Instance.SwitchToNextState();
            }
        }

        //按下 0 切换到禁用状态 (PlaceMode 0: 禁用动作)
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
        {
            ProcessManager.Instance.SetPlacementMode(0);
            gestureSpawnSelector.ApplyRecognizedLabel("C");     //预设为空物体
            Debug.Log("Switched to Place Mode 0: Disabled");
        }

        // 按下 1 切换到放置状态 (PlaceMode 1: 放置松饼)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            ProcessManager.Instance.SetPlacementMode(1);
            Debug.Log("Switched to Place Mode 1: Placement");
        }

        // 按下 2 切换到写/手势识别状态 (State 2: 放果酱)
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            ProcessManager.Instance.SetPlacementMode(2);
            Debug.Log("Switched to Place Mode 2: Gesture Recognition");
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            handSpawnController.SpawnAtCurrentPoint();
        }

        //选择水果时，加入键盘控制
        if (Input.GetKeyDown(KeyCode.W) && ProcessManager.Instance.IsGestureMode())
        {
            gestureSpawnSelector.ApplyRecognizedLabel("1");     //切到草莓预设
        }

        // if (Input.GetKeyDown(KeyCode.S) && ProcessManager.Instance.IsGestureMode())
        // {
        //     gestureSpawnSelector.ApplyRecognizedLabel("0");     //切回默认松饼预设
        // }

        //键盘手动调整放置位置在HandSpawnController里实现
    }

    //处理手势识别
}
