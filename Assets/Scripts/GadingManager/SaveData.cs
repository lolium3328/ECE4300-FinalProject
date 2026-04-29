using System;

[Serializable] // 必须加上这个特性，JsonUtility 才能识别
public class SaveData
{
    public int highestScore; // 我们要存的整数分数
}