using System.Collections.Generic;
using UnityEngine;

public class CreamSurfacePlacementTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject creamClusterPrefab;
    [SerializeField] private Transform placementCursor;
    [SerializeField] private Transform spawnParent;

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

    private readonly List<GameObject> spawnedCream = new List<GameObject>();
    private Vector3 cursorPosition;
    private bool hasSurfaceHit;
    private RaycastHit lastHit;

    private void Awake()
    {
        Transform cursor = placementCursor != null ? placementCursor : transform;
        cursorPosition = cursor.position;
        UpdatePlacementCursor();
    }

    private void Update()
    {
        MoveCursorFromKeyboard();
        UpdatePlacementCursor();

        if (Input.GetKeyDown(spawnKey))
        {
            SpawnCream();
        }

        if (Input.GetKeyDown(clearKey))
        {
            ClearSpawnedCream();
        }
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
            return;
        }

        placementCursor.position = lastHit.point + lastHit.normal * surfaceOffset;

        if (alignToSurfaceNormal)
        {
            placementCursor.rotation = Quaternion.FromToRotation(Vector3.up, lastHit.normal);
        }
    }

    private void SpawnCream()
    {
        if (creamClusterPrefab == null)
        {
            Debug.LogWarning("[CreamSurfacePlacementTester] Cream cluster prefab is not assigned.", this);
            return;
        }

        if (!hasSurfaceHit)
        {
            Debug.LogWarning("[CreamSurfacePlacementTester] No valid surface found under the placement cursor.", this);
            return;
        }

        Vector3 spawnPosition = lastHit.point + lastHit.normal * surfaceOffset;
        Quaternion spawnRotation = alignToSurfaceNormal
            ? Quaternion.FromToRotation(Vector3.up, lastHit.normal)
            : Quaternion.identity;

        GameObject instance = Instantiate(creamClusterPrefab, spawnPosition, spawnRotation, spawnParent);
        spawnedCream.Add(instance);
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
