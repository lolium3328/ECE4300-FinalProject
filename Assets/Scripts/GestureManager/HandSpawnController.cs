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
    [SerializeField] private float followSpeedKeyboard = 0.5f;   // 键盘调整的跟随速度
    private float waitTimer = 0f;
    public bool autoExpandInputRange = true;

    [Header("Spawn")]
    public GameObject previewPrefab;
    public GameObject prefabToSpawn;
    public Material previewMaterial;
    public Transform spawnParent;
    public float spawnCooldown = 0.2f;
    public Vector3 spawnEulerOffset = Vector3.zero;
    [Range(0f, 1f)]
    public float previewIdleAlpha = 0.5f;
    [Range(0f, 1f)]
    public float previewHighlightAlpha = 1f;
    public float previewHighlightDuration = 2f;

    private float _currentX;
    private float _currentXApply;
    private float _currentXDelta = 0f;
    private float _lastSpawnTime = -999f;
    private GameObject _previewInstance;
    private Renderer[] _previewRenderers;
    private Coroutine _previewAlphaRoutine;

    [SerializeField] GestureSpawnSelector gestureSpawnSelector;

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
        EnsurePreviewInstance();
    }

    private void Update()   //处理键盘手动调整放置位置
    {
        bool isPlacementActive = IsPlacementModuleActive();
        SetPreviewVisible(isPlacementActive);
        if (!isPlacementActive)
        {
            return;
        }

        _currentXApply = _currentX + _currentXDelta;
        ApplyPointPosition(_currentXApply);

        //加入键盘的手动调整
        if (Input.GetKey(KeyCode.D))
        {
            waitTimer = 0f; // 重置计时器，保持在有输入状态
            _currentXDelta += followSpeedKeyboard * Time.deltaTime;
            ApplyPointPosition(_currentXApply);
        }
        if (Input.GetKey(KeyCode.A))
        {
            waitTimer = 0f; // 重置计时器，保持在有输入状态
            _currentXDelta -= followSpeedKeyboard * Time.deltaTime;
            ApplyPointPosition(_currentXApply);
        }
        if (Input.GetKeyUp(KeyCode.S))
        {
            waitTimer = 0f; // 重置计时器，准备在放开后逐渐恢复
        }
        {
            waitTimer += Time.deltaTime;
            // 如果2秒内没有按键输入，逐渐恢复到默认位置
            if (waitTimer < 2f)
            {
                return;
            }
            if (Mathf.Abs(_currentXDelta) > 0.01f)
            {
                _currentXDelta = Mathf.Lerp(_currentXDelta, 0f, 1f * Time.deltaTime);
                ApplyPointPosition(_currentXApply);
            }
        }
        
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
        // if (movingPoint == null)
        // {
        //     return;
        // }

        movingPoint.position = new Vector3(x, fixedY, fixedZ);
    }

    private void EnsurePreviewInstance()
    {
        if (_previewInstance != null || movingPoint == null)
        {
            return;
        }

        GameObject previewSource = previewPrefab != null ? previewPrefab : prefabToSpawn;
        if (previewSource == null)
        {
            return;
        }

        _previewInstance = Instantiate(previewSource, movingPoint);
        _previewInstance.name = previewSource.name + "_Preview";
        _previewInstance.transform.localPosition = Vector3.zero;
        _previewInstance.transform.localRotation = Quaternion.Euler(spawnEulerOffset);
        _previewInstance.transform.localScale = previewSource.transform.localScale;

        foreach (Collider col in _previewInstance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        foreach (Rigidbody body in _previewInstance.GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>(true);
        if (previewMaterial != null)
        {
            foreach (Renderer previewRenderer in _previewRenderers)
            {
                if (previewRenderer == null)
                {
                    continue;
                }

                previewRenderer.material = previewMaterial;
            }
        }

        SetPreviewAlpha(previewIdleAlpha);
        SetPreviewVisible(IsPlacementModuleActive());
    }

    public void SetPrefabToSpawn(GameObject newPrefab)  // 供外部调用以更改当前生成物体的预设,例如 GestureSpawnSelector 根据识别结果切换预设
    {
        if (newPrefab == null)
        {
            Debug.LogWarning("[HandSpawnController] SetPrefabToSpawn received a null prefab.");
            return;
        }

        GameObject previousSpawnPrefab = prefabToSpawn;
        prefabToSpawn = newPrefab;

        // Keep the preview in sync when it was following the old spawn prefab.
        if (previewPrefab == null || previewPrefab == previousSpawnPrefab)
        {
            previewPrefab = newPrefab;
        }

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_previewAlphaRoutine != null)
        {
            StopCoroutine(_previewAlphaRoutine);
            _previewAlphaRoutine = null;
        }

        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }

        _previewRenderers = null;
        EnsurePreviewInstance();
        SetPreviewVisible(IsPlacementModuleActive());
    }

    private void SetPreviewVisible(bool isVisible)
    {
        if (_previewInstance != null && _previewInstance.activeSelf != isVisible)
        {
            _previewInstance.SetActive(isVisible);
        }
    }

    private void SetPreviewAlpha(float alpha)
    {
        if (_previewRenderers == null)
        {
            return;
        }

        float clampedAlpha = Mathf.Clamp01(alpha);
        foreach (Renderer previewRenderer in _previewRenderers)
        {
            if (previewRenderer == null)
            {
                continue;
            }

            Material[] materials = previewRenderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    Color baseColor = material.GetColor("_BaseColor");
                    baseColor.a = clampedAlpha;
                    material.SetColor("_BaseColor", baseColor);
                }

                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = clampedAlpha;
                    material.color = color;
                }
            }
        }
    }

    private void PlayPreviewHighlight()
    {
        EnsurePreviewInstance();
        if (_previewInstance == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (_previewAlphaRoutine != null)
        {
            StopCoroutine(_previewAlphaRoutine);
        }

        _previewAlphaRoutine = StartCoroutine(AnimatePreviewAlpha(previewIdleAlpha, previewHighlightAlpha, previewHighlightDuration));
    }

    private System.Collections.IEnumerator AnimatePreviewAlpha(float fromAlpha, float toAlpha, float duration)
    {
        if (duration <= 0f)
        {
            SetPreviewAlpha(toAlpha);
            _previewAlphaRoutine = null;
            yield break;
        }

        SetPreviewAlpha(fromAlpha);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetPreviewAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetPreviewAlpha(toAlpha);
        _previewAlphaRoutine = null;
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
        Debug.Log("[HandSpawnController] SpawnAtCurrentPoint called.");
        if (!IsPlacementModuleActive())
        {
             Debug.Log("[HandSpawnController] 当前不处于放置模式，已取消生成。");
            return;
        }
        if (prefabToSpawn == null || movingPoint == null)
        {
            Debug.Log("[HandSpawnController] prefabToSpawn 或 movingPoint 为空，已取消生成。");
            return;
        }
        // 防连发冷却检查
        if (Time.time - _lastSpawnTime < spawnCooldown)
        {
            Debug.Log("[HandSpawnController] 生成冷却中，已取消生成。");
            return;
        }
        if (!PrefabIdentity.TryGetIdentity(prefabToSpawn.transform, out PrefabIdentity prefabIdentity))
        {
            Debug.LogWarning("[HandSpawnController] prefabToSpawn 缺少 PrefabIdentity，已取消生成。", prefabToSpawn);
            return;
        }

        if (SpawnLimitManager.Instance != null && !SpawnLimitManager.Instance.CanSpawn(prefabIdentity))
        {
            Debug.Log($"[HandSpawnController] {prefabIdentity.Type} 已达到最大生成数量。");
            return;
        }

        _lastSpawnTime = Time.time;
        Transform actualSpawnParent = spawnParent == movingPoint ? null : spawnParent;
        Quaternion spawnRotation = Quaternion.Euler(spawnEulerOffset);
       
        GameObject spawnedObject = Instantiate(prefabToSpawn, movingPoint.position, spawnRotation, actualSpawnParent);
         Debug.Log("[HandSpawnController] ");
        if (spawnedObject.GetComponent<SpawnedObjectLife>() == null)
        {
            spawnedObject.AddComponent<SpawnedObjectLife>();
        }

        if (SpawnLimitManager.Instance != null)
        {
            bool registered = SpawnLimitManager.Instance.RegisterSpawn(spawnedObject);
            if (!registered)
            {
                Debug.LogWarning("[HandSpawnController] 生成后的对象登记失败，已销毁该实例。", spawnedObject);
                Destroy(spawnedObject);
                return;
            }
        }

        PlayPreviewHighlight();
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
        if (frame == null)
        {
            Debug.LogWarning("[HandSpawnController] Leap Motion 未连接，无法校准输入范围。");
            return;
        }

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
        if (ProcessManager.Instance != null)
        {
            return ProcessManager.Instance.IsPlacementMode() && ProcessManager.Instance.State != 4;
        }

        return true;
    }
}
