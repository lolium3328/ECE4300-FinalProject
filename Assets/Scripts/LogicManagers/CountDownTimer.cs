using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CircleCountdown : MonoBehaviour
{
    [SerializeField] private GameObject countdownTimerObj;
    [SerializeField] private Image fillCircle;           // 拖入 Fill_Circle
    [SerializeField] private float totalTime = 0f;

    [Header("颜色设置")]
    [SerializeField] private Color normalColor = Color.green;    // 初始颜色
    [SerializeField] private Color warningColor = Color.red;     // 警告颜色
    [SerializeField] private float warningThreshold = 3f;        // 触发警告的时间阈值

    private float remainingTime;

    public void StartCountdown(float t)     // 外部接口，激活一个 t 秒的倒计时动画
    {
        totalTime = t;
        remainingTime = totalTime;
        fillCircle.fillAmount = 1f;  // 圆环满
        fillCircle.color = normalColor;  // 使用配置的初始颜色

        countdownTimerObj.SetActive(true);  // 显示倒计时 UI
    }

    private void Start()
    {
        remainingTime = totalTime;
    }

    void Update()
    {
        if (remainingTime > 0)
        {
            remainingTime -= Time.deltaTime;
            remainingTime = Mathf.Max(0, remainingTime);

            // 更新圆环
            fillCircle.fillAmount = remainingTime / totalTime;

            // 根据剩余时间切换颜色
            if (remainingTime <= warningThreshold)
            {
                fillCircle.color = warningColor;
            }
            else
            {
                fillCircle.color = normalColor;
            }
        }
        else    // 隐藏当前 object，等下一次 StartCountdown 被调用
        {
            countdownTimerObj.SetActive(false);
        }
    }
}