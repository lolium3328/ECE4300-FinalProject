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
    [Tooltip("Ultraleap 的手部数据来源。这里通常拖入场景里的 Leap Provider。")]
    public LeapProvider leapProvider;
    [Tooltip("右手用哪个部位来驱动 x 坐标。Palm 是手掌中心，IndexTip 是食指指尖。")]
    public RightHandTrackTarget trackTarget = RightHandTrackTarget.IndexTip;

    [Header("Tracked Point")]
    [Tooltip("场景里真正被移动的点。这个点会被脚本更新位置，并作为生成物体的参考点。")]
    public Transform movingPoint;
    [Tooltip("固定生成点的 y 坐标。无论右手怎么动，这个点的 y 都保持这个值。")]
    public float fixedY = 0.5f;
    [Tooltip("固定生成点的 z 坐标。无论右手怎么动，这个点的 z 都保持这个值。")]
    public float fixedZ = 0f;
    [Tooltip("右手原始 x 输入范围的左边界。归一化模式下，手移动到这里会映射到 Min X。")]
    public float inputMinX = -0.2f;
    [Tooltip("右手原始 x 输入范围的右边界。归一化模式下，手移动到这里会映射到 Max X。")]
    public float inputMaxX = 0.2f;
    [Tooltip("场景里移动点允许达到的最小 x。归一化之后的结果会映射到这个左边界。")]
    public float minX = -1f;
    [Tooltip("场景里移动点允许达到的最大 x。归一化之后的结果会映射到这个右边界。")]
    public float maxX = 1f;
    [Range(0f, 30f)]
    [Tooltip("点跟随右手的速度。值越大越灵敏，值越小越平滑。")]
    public float followSpeed = 15f;
    [Tooltip("开启后会在运行时自动扩展 Input Min X / Input Max X，适合先自由摆手做范围标定。")]
    public bool autoExpandInputRange = true;

    [Header("Spawn")]
    [Tooltip("左手触发生成时，要实例化出来的预制体。")]
    public GameObject prefabToSpawn;
    [Tooltip("预览物体的材质（可选）。如果不填，将直接使用原预制体作为预览。")]
    public Material previewMaterial;
    [Tooltip("新生成物体的父物体。留空则直接生成在场景根节点下。")]
    public Transform spawnParent;
    [Tooltip("两次生成之间的最小间隔时间，防止手势持续识别时一帧生成一个。")]
    public float spawnCooldown = 0.2f;

    private float _currentX;
    private float _lastSpawnTime = -999f;
    private GameObject _previewInstance;

    private void Reset()
    {
        movingPoint = transform;
    }

    private void Awake()
    {
        if (movingPoint == null)
        {
            movingPoint = transform;
        }

        Vector3 startPosition = movingPoint.position;
        _currentX = startPosition.x;
        ApplyPointPosition(_currentX);

        CreatePreview();
    }

    private void CreatePreview()
    {
        if (prefabToSpawn == null) return;

        // 创建预览实例
        _previewInstance = Instantiate(prefabToSpawn, movingPoint.position, Quaternion.identity, movingPoint);
        _previewInstance.name = "PlacementPreview";

        // 移除或禁用预览物体的碰撞体，防止物理引擎干扰
        foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        // 如果设置了预览材质（比如半透明），则应用它
        if (previewMaterial != null)
        {
            foreach (var renderer in _previewInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.material = previewMaterial;
            }
        }

        _previewInstance.SetActive(false);
    }

    private void OnEnable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame += OnUpdateFrame;
        }
    }

    private void OnDisable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame -= OnUpdateFrame;
        }
    }

    private void OnUpdateFrame(Frame frame)
    {
        if (!IsPlacementModuleActive())
        {
            if (_previewInstance != null) _previewInstance.SetActive(false);
            return;
        }

        Hand rightHand = frame.GetHand(Chirality.Right);
        if (rightHand == null)
        {
            if (_previewInstance != null) _previewInstance.SetActive(false);
            return;
        }

        if (_previewInstance != null) _previewInstance.SetActive(true);

        float rawX = GetTrackedX(rightHand);

        if (autoExpandInputRange)
        {
            inputMinX = Mathf.Min(inputMinX, rawX);
            inputMaxX = Mathf.Max(inputMaxX, rawX);
        }

        float targetX = NormalizeAndMapX(rawX);

        _currentX = Mathf.Lerp(_currentX, targetX, followSpeed * Time.deltaTime);
        ApplyPointPosition(_currentX);
    }

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

    private void ApplyPointPosition(float x)
    {
        if (movingPoint == null)
        {
            return;
        }

        movingPoint.position = new Vector3(x, fixedY, fixedZ);
    }

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

        if (Time.time - _lastSpawnTime < spawnCooldown)
        {
            return;
        }

        _lastSpawnTime = Time.time;
        Instantiate(prefabToSpawn, movingPoint.position, Quaternion.identity, spawnParent);
    }

    public void SetFixedYZFromCurrentPoint()
    {
        if (movingPoint == null)
        {
            return;
        }

        fixedY = movingPoint.position.y;
        fixedZ = movingPoint.position.z;
    }

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

    private bool IsPlacementModuleActive()
    {
        if (TestForGest.Instance != null)
        {
            return TestForGest.Instance.IsPlacementMode();
        }

        if (ProcessManager.Instance == null)
        {
            return true;
        }

        return ProcessManager.Instance.State == 3;
    }
}
