using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CircleCountdown : MonoBehaviour
{
    [SerializeField] private GameObject countdownTimerObj;
    [SerializeField] private Image fillCircle;           // 拖入 Fill_Circle
    [SerializeField] private TextMeshProUGUI timerText;  // 拖入 Timer_Text
    [SerializeField] private float totalTime = 0f;

    public void StartCountdown(float t)     //外部接口，激活一个t秒的倒计时动画
    {
        totalTime = t;
        remainingTime = totalTime;
        fillCircle.fillAmount = 1f;  // 圆环满
        fillCircle.color = Color.green;  // 圆环颜色恢复为绿色

        countdownTimerObj.SetActive(true);  // 显示倒计时UI
    }

    private float remainingTime;

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

            // 更新数字
            timerText.text = Mathf.CeilToInt(remainingTime).ToString();

            // 最后3秒变红
            if (remainingTime <= 3f)
            {
                fillCircle.color = Color.red;
            }
        }
        else    //隐藏当前object，等下一次StartCountdown被调用
        {
            countdownTimerObj.SetActive(false);
        }
    }
}