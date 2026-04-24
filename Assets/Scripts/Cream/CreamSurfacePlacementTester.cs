using System.Collections.Generic;
using Leap;
using UnityEngine;

public class CreamSurfacePlacementTester : MonoBehaviour
{
    private enum HandTrackTarget
    {
        Palm,
        IndexTip
    }

    [Header("References")]
    [SerializeField] private GameObject creamClusterPrefab;
    [SerializeField] private Transform placementCursor;
    [SerializeField] private Transform spawnParent;
    [SerializeField] private bool inputEnabled = true;

    [Header("Leap Input")]
    [SerializeField] private LeapProvider leapProvider;
    [SerializeField] private Chirality handType = Chirality.Right;
    [SerializeField] private HandTrackTarget trackTarget = HandTrackTarget.IndexTip;
    [SerializeField] private bool enableLeapInput = true;
    [SerializeField] private bool autoFindLeapProvider = true;
    [SerializeField] private bool autoExpandInputRange = true;
    [SerializeField] private bool invertLeapZ;
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float leapMappingLogInterval = 0.25f;
    [SerializeField] private float inputMinX = -0.2f;
    [SerializeField] private float inputMaxX = 0.2f;
    [SerializeField] private float inputMinZ = -0.2f;
    [SerializeField] private float inputMaxZ = 0.2f;
    [SerializeField] private Vector2 sceneMinXZ = new Vector2(-0.2f, -0.25f);
    [SerializeField] private Vector2 sceneMaxXZ = new Vector2(0.66f, 0.45f);

    [Header("Surface Raycast")]
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField] private float rayStartHeight = 0.5f;
    [SerializeField] private float rayDistance = 2f;
    [SerializeField] private float surfaceOffset = 0.01f;
    [SerializeField] private bool alignToSurfaceNormal = true;

    [Header("Keyboard Controls")]
    [SerializeField] private float moveSpeed = 0.35f;
    [SerializeField] private KeyCode spawnKey = KeyCode.F;
    [SerializeField] private KeyCode clearKey = KeyCode.Backspace;
    [SerializeField] private float continuousSpawnInterval = 0.25f;

    private readonly List<GameObject> spawnedCream = new List<GameObject>();
    private Vector3 cursorPosition;
    private bool hasSurfaceHit;
    private RaycastHit lastHit;
    private float nextSpawnTime;
    private float nextLeapMappingLogTime;

    public GameObject CurrentPrefabToSpawn => creamClusterPrefab;

    private void Awake()
    {
        Transform cursor = placementCursor != null ? placementCursor : transform;
        cursorPosition = cursor.position;
        UpdatePlacementCursor();
        ApplyCursorVisibility();
    }

    private void OnEnable()
    {
        EnsureLeapProvider();

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

    private void Update()
    {
        if (!inputEnabled)
        {
            return;
        }

        MoveCursorFromKeyboard();
        UpdatePlacementCursor();

        if (Input.GetKey(spawnKey))
        {
            SpawnCreamAtCurrentSurfaceWithInterval();
        }

        if (Input.GetKeyDown(clearKey))
        {
            ClearSpawnedCream();
        }
    }

    private void EnsureLeapProvider()
    {
        if (leapProvider != null || !autoFindLeapProvider)
        {
            return;
        }

        leapProvider = FindObjectOfType<LeapProvider>();
    }

    private void OnUpdateFrame(Frame frame)
    {
        if (!inputEnabled || !enableLeapInput || frame == null)
        {
            return;
        }

        Hand hand = frame.GetHand(handType);
        if (hand == null)
        {
            return;
        }

        Vector3 rawPosition = GetTrackedPosition(hand);

        if (autoExpandInputRange)
        {
            inputMinX = Mathf.Min(inputMinX, rawPosition.x);
            inputMaxX = Mathf.Max(inputMaxX, rawPosition.x);
            inputMinZ = Mathf.Min(inputMinZ, rawPosition.z);
            inputMaxZ = Mathf.Max(inputMaxZ, rawPosition.z);
        }

        float normalizedX = NormalizeLeapAxis(rawPosition.x, inputMinX, inputMaxX, false);
        float normalizedZ = NormalizeLeapAxis(rawPosition.z, inputMinZ, inputMaxZ, invertLeapZ);
        float targetX = Mathf.Lerp(sceneMinXZ.x, sceneMaxXZ.x, normalizedX);
        float targetZ = Mathf.Lerp(sceneMinXZ.y, sceneMaxXZ.y, normalizedZ);
        Vector3 targetPosition = new Vector3(targetX, cursorPosition.y, targetZ);
        cursorPosition = Vector3.Lerp(cursorPosition, targetPosition, Mathf.Clamp01(followSpeed * Time.deltaTime));
        LogLeapAndCursorPosition(rawPosition);
    }

    private Vector3 GetTrackedPosition(Hand hand)
    {
        if (trackTarget == HandTrackTarget.Palm)
        {
            return hand.PalmPosition;
        }

        Finger indexFinger = hand.Index;
        return indexFinger != null ? indexFinger.TipPosition : hand.PalmPosition;
    }

    private static float NormalizeLeapAxis(float rawValue, float inputMin, float inputMax, bool invert)
    {
        float inputRange = inputMax - inputMin;
        if (Mathf.Abs(inputRange) < 0.0001f)
        {
            return 0f;
        }

        float normalized = Mathf.InverseLerp(inputMin, inputMax, rawValue);
        if (invert)
        {
            normalized = 1f - normalized;
        }

        return normalized;
    }

    private void LogLeapAndCursorPosition(Vector3 rawPosition)
    {
        if (Time.time < nextLeapMappingLogTime)
        {
            return;
        }

        nextLeapMappingLogTime = Time.time + Mathf.Max(0.01f, leapMappingLogInterval);
        string visibleCursorText = placementCursor != null
            ? $"visibleCursor=({placementCursor.position.x:F3}, {placementCursor.position.y:F3}, {placementCursor.position.z:F3})"
            : "visibleCursor=null";

        Debug.Log(
            $"[CreamSurfacePlacementTester] Hand xyz=({rawPosition.x:F3}, {rawPosition.y:F3}, {rawPosition.z:F3}) " +
            $"cursor=({cursorPosition.x:F3}, {cursorPosition.y:F3}, {cursorPosition.z:F3}) " +
            $"{visibleCursorText} surfaceHit={hasSurfaceHit}",
            this);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (enabled)
        {
            UpdatePlacementCursor();
        }

        ApplyCursorVisibility();
    }

    public void SetPrefabToSpawn(GameObject newPrefab)
    {
        if (newPrefab == null)
        {
            Debug.LogWarning("[CreamSurfacePlacementTester] SetPrefabToSpawn received a null prefab.", this);
            return;
        }

        creamClusterPrefab = newPrefab;
    }

    public void EnableInput()
    {
        SetInputEnabled(true);
    }

    public void DisableInput()
    {
        SetInputEnabled(false);
    }

    public void CalibrateInputRangeFromCurrentHand()
    {
        EnsureLeapProvider();
        if (leapProvider == null || leapProvider.CurrentFrame == null)
        {
            Debug.LogWarning("[CreamSurfacePlacementTester] LeapProvider is missing, cannot calibrate hand input range.", this);
            return;
        }

        Hand hand = leapProvider.CurrentFrame.GetHand(handType);
        if (hand == null)
        {
            Debug.LogWarning("[CreamSurfacePlacementTester] Target hand is missing, cannot calibrate hand input range.", this);
            return;
        }

        Vector3 rawPosition = GetTrackedPosition(hand);
        float halfX = Mathf.Max(0.01f, (inputMaxX - inputMinX) * 0.5f);
        float halfZ = Mathf.Max(0.01f, (inputMaxZ - inputMinZ) * 0.5f);
        inputMinX = rawPosition.x - halfX;
        inputMaxX = rawPosition.x + halfX;
        inputMinZ = rawPosition.z - halfZ;
        inputMaxZ = rawPosition.z + halfZ;
    }

    public void SpawnCreamAtCurrentSurfaceEvent()
    {
        if (!inputEnabled)
        {
            return;
        }

        SpawnCream();
    }

    public void SpawnCreamAtCurrentSurfaceWithIntervalEvent()
    {
        if (!inputEnabled)
        {
            return;
        }

        SpawnCreamAtCurrentSurfaceWithInterval();
    }

    private bool SpawnCreamAtCurrentSurfaceWithInterval()
    {
        if (Time.time < nextSpawnTime)
        {
            return false;
        }

        bool spawned = SpawnCream();
        if (spawned)
        {
            nextSpawnTime = Time.time + Mathf.Max(0.01f, continuousSpawnInterval);
        }

        return spawned;
    }

    private void MoveCursorFromKeyboard()
    {
        Vector3 input = Vector3.zero;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            input.x -= 1f;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            input.x += 1f;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            input.z -= 1f;
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            input.z += 1f;
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        cursorPosition += input * moveSpeed * Time.deltaTime;
    }

    private void UpdatePlacementCursor()
    {
        Vector3 rayOrigin = cursorPosition + Vector3.up * rayStartHeight;
        hasSurfaceHit = Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out lastHit,
            rayDistance,
            surfaceMask,
            QueryTriggerInteraction.Ignore);

        if (placementCursor == null || !hasSurfaceHit)
        {
            ApplyCursorVisibility();
            return;
        }

        placementCursor.position = lastHit.point + lastHit.normal * surfaceOffset;

        if (alignToSurfaceNormal)
        {
            placementCursor.rotation = Quaternion.FromToRotation(Vector3.up, lastHit.normal);
        }

        ApplyCursorVisibility();
    }

    private void ApplyCursorVisibility()
    {
        if (placementCursor == null)
        {
            return;
        }

        placementCursor.gameObject.SetActive(inputEnabled && hasSurfaceHit);
    }

    private bool SpawnCream()
    {
        if (creamClusterPrefab == null)
        {
            Debug.LogWarning("[CreamSurfacePlacementTester] Cream cluster prefab is not assigned.", this);
            return false;
        }

        if (!hasSurfaceHit)
        {
            Debug.LogWarning("[CreamSurfacePlacementTester] No valid surface found under the placement cursor.", this);
            return false;
        }

        Vector3 spawnPosition = lastHit.point + lastHit.normal * surfaceOffset;
        Quaternion spawnRotation = alignToSurfaceNormal
            ? Quaternion.FromToRotation(Vector3.up, lastHit.normal)
            : Quaternion.identity;

        GameObject instance = Instantiate(creamClusterPrefab, spawnPosition, spawnRotation, spawnParent);
        spawnedCream.Add(instance);
        return true;
    }

    private void ClearSpawnedCream()
    {
        for (int i = spawnedCream.Count - 1; i >= 0; i--)
        {
            GameObject target = spawnedCream[i];
            if (target != null)
            {
                Destroy(target);
            }
        }

        spawnedCream.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying
            ? cursorPosition + Vector3.up * rayStartHeight
            : transform.position + Vector3.up * rayStartHeight;

        Gizmos.color = hasSurfaceHit ? Color.green : Color.yellow;
        Gizmos.DrawLine(origin, origin + Vector3.down * rayDistance);

        if (Application.isPlaying && hasSurfaceHit)
        {
            Gizmos.DrawWireSphere(lastHit.point, 0.025f);
        }
    }
}
