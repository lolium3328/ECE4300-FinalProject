using Leap;
using UnityEngine;

public class HandSpawnController : MonoBehaviour
{
    public enum RightHandTrackTarget
    {
        Palm,
        IndexTip
    }

    [Header("Leap Input")]
    public LeapProvider leapProvider;
    public RightHandTrackTarget trackTarget = RightHandTrackTarget.IndexTip;

    [Header("Tracked Point")]
    public Transform movingPoint;
    public float fixedY = 0.5f;
    public float fixedZ = 0f;
    public float inputMinX = -0.2f;
    public float inputMaxX = 0.2f;
    public float minX = -1f;
    public float maxX = 1f;
    [Range(0f, 30f)]
    public float followSpeed = 15f;
    public bool autoExpandInputRange = true;

    [Header("Spawn")]
    public GameObject prefabToSpawn;
    public Material previewMaterial;
    public Transform spawnParent;
    public float spawnCooldown = 0.2f;

    private float _currentX;
    private float _lastSpawnTime = -999f;
    private GameObject _previewInstance;

    /// <summary>
    /// 当脚本在 Inspector 中被重置或添加时调用，默认将当前物体设为移动参考点。
    /// </summary>
    private void Reset()
    {
        movingPoint = transform;
    }

    /// <summary>
    /// 初始化：确定参考点位置并创建预览物体。
    /// </summary>
    private void Awake()
    {
        if (movingPoint == null)
        {
            movingPoint = transform;
        }

        Vector3 startPosition = movingPoint.position;
        _currentX = startPosition.x;
        ApplyPointPosition(_currentX);
    }

    /// <summary>
    /// 启用脚本时，订阅 LeapProvider 的帧更新事件。
    /// </summary>
    private void OnEnable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame += OnUpdateFrame;
        }
    }

    /// <summary>
    /// 禁用脚本时，取消订阅。
    /// </summary>
    private void OnDisable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame -= OnUpdateFrame;
        }
    }

    /// <summary>
    /// 每帧执行：检测当前是否处于放置模式，获取右手位置并平滑移动预览点。
    /// </summary>
    private void OnUpdateFrame(Frame frame)
    {
        // 检查当前模式是否为放置模式
        if (!IsPlacementModuleActive())
        {
            return;
        }

        Hand rightHand = frame.GetHand(Chirality.Right);
        if (rightHand == null)
        {
            return;
        }

        float rawX = GetTrackedX(rightHand);

        // 如果开启自动扩展，根据手拉伸的极限自动调整输入范围
        if (autoExpandInputRange)
        {
            inputMinX = Mathf.Min(inputMinX, rawX);
            inputMaxX = Mathf.Max(inputMaxX, rawX);
        }

        // 将手部物理坐标映射到场景坐标
        float targetX = NormalizeAndMapX(rawX);
        _currentX = Mathf.Lerp(_currentX, targetX, followSpeed * Time.deltaTime);
        ApplyPointPosition(_currentX);
    }

    /// <summary>
    /// 根据配置获取右手的目标 X 轴坐标（手掌或食指尖）。
    /// </summary>
    private float GetTrackedX(Hand rightHand)
    {
        if (trackTarget == RightHandTrackTarget.Palm)
        {
            return rightHand.PalmPosition.x;
        }

        Finger indexFinger = rightHand.Index;
        if (indexFinger != null)
        {
            return indexFinger.TipPosition.x;
        }

        return rightHand.PalmPosition.x;
    }

    /// <summary>
    /// 更新参考点 Transform 的实际场景位置，锁定 Y 和 Z 轴。
    /// </summary>
    private void ApplyPointPosition(float x)
    {
        if (movingPoint == null)
        {
            return;
        }

        movingPoint.position = new Vector3(x, fixedY, fixedZ);
    }

    /// <summary>
    /// 将手部的原始 X 坐标映射到场景设定的 MinX 和 MaxX 范围内。
    /// </summary>
    private float NormalizeAndMapX(float rawX)
    {
        float inputRange = inputMaxX - inputMinX;
        if (Mathf.Abs(inputRange) < 0.0001f)
        {
            return minX;
        }

        float normalizedX = Mathf.InverseLerp(inputMinX, inputMaxX, rawX);
        return Mathf.Lerp(minX, maxX, normalizedX);
    }

    /// <summary>
    /// 执行实例化：在当前参考点位置克隆生成物体。
    /// </summary>
    public void SpawnAtCurrentPoint()
    {
        if (!IsPlacementModuleActive())
        {
            return;
        }

        if (prefabToSpawn == null || movingPoint == null)
        {
            return;
        }

        // 防连发冷却检查
        if (Time.time - _lastSpawnTime < spawnCooldown)
        {
            return;
        }

        _lastSpawnTime = Time.time;
        Instantiate(prefabToSpawn, movingPoint.position, Quaternion.identity, spawnParent);
    }

    /// <summary>
    /// 将当前的锁定位置 Y 和 Z 设置为参考点目前的实时坐标。
    /// </summary>
    public void SetFixedYZFromCurrentPoint()
    {
        if (movingPoint == null)
        {
            return;
        }

        fixedY = movingPoint.position.y;
        fixedZ = movingPoint.position.z;
    }

    /// <summary>
    /// 根据当前右手的实时位置快速重置输入范围边界。
    /// </summary>
    public void CalibrateInputRangeFromCurrentHand()
    {
        if (leapProvider == null)
        {
            return;
        }

        Frame frame = leapProvider.CurrentFrame;
        Hand rightHand = frame.GetHand(Chirality.Right);
        if (rightHand == null)
        {
            return;
        }

        float rawX = GetTrackedX(rightHand);
        inputMinX = rawX;
        inputMaxX = rawX;
    }

    /// <summary>
    /// 核心判断：询问 TestForGest 单例当前是否应该开启“放置功能”。
    /// </summary>
    private bool IsPlacementModuleActive()
    {
        if (TestForGest.Instance != null)
        {
            return TestForGest.Instance.IsPlacementMode();
        }

        return true;
    }
}
