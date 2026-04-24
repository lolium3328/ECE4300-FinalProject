using UnityEngine;

public class PrefabToSpawn : MonoBehaviour
{
    private void Update()
    {
        if (ProcessManager.Instance != null && ProcessManager.Instance.State == 2)
        {
            Destroy(gameObject);    //在重新回到state 2时销毁预设对象，避免旧松饼干扰玩家操作
        }
    }
}
