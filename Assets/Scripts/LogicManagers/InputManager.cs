using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }   //做成了全局静态实例，其他脚本通过InputManager.Instance直接访问

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
            ProcessManager.Instance.SwitchToNextState();
        }
    }

    //处理手势识别
}
