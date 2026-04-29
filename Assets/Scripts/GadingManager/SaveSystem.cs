using UnityEngine;
using System.IO; // 必须引用 IO

public static class SaveSystem
{
    // 定义文件保存路径 (Application.persistentDataPath 会根据平台自动选择位置)
    private static string SavePath => Path.Combine(Application.persistentDataPath, "highscore.json");

    // 保存分数
    public static void Save(int score)
    {
        SaveData data = new SaveData { highestScore = score };
        
        // 将对象转换为 JSON 字符串
        // true 参数表示“漂亮打印”，会让生成的 JSON 文件易于阅读
        string json = JsonUtility.ToJson(data, true);

        // 写入文件
        File.WriteAllText(SavePath, json);
        Debug.Log("分数已保存到: " + SavePath);
    }

    // 读取分数
    public static int Load()
    {
        if (File.Exists(SavePath))
        {
            // 读取文件内容
            string json = File.ReadAllText(SavePath);
            
            // 将 JSON 字符串转回对象
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            Debug.Log("Load highest score:" + json);
            return data.highestScore;
        }
        else
        {
            // 如果文件不存在，返回默认值 0
            Debug.Log("json file does not exist");
            return 0;
        }
    }
}