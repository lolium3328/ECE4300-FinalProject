using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    private int highestScore;

    private void Start()
    {
        // 1. 游戏启动时读取最高分
        highestScore = SaveSystem.Load();
    }

    public int GetHighestScore()
    {
        highestScore = SaveSystem.Load();
        return highestScore;
    }

    // 当游戏结束或者分数变化时调用
    public void UpdateHighScore(int currentScore)
    {
        if (currentScore > highestScore)
        {
            highestScore = currentScore;
            
            // 2. 只有破纪录时才写入文件
            SaveSystem.Save(highestScore);
            Debug.Log("新纪录！");
        }
    }

    // // 测试用的方法：按下键盘 G 模拟保存
    // private void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.G))
    //     {
    //         UpdateHighScore(Random.Range(0, 100));
    //     }
    // }
}