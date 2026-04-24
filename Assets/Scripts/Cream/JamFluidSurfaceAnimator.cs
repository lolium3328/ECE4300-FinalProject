using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class JamFluidSurfaceAnimator : MonoBehaviour
{
    [SerializeField] private float spreadDuration = 0.65f;
    [SerializeField] private float waveAmplitude = 0.0035f;
    [SerializeField] private float waveFrequency = 3.2f;
    [SerializeField] private float edgeFlowAmplitude = 0.055f;
    [SerializeField] private bool adhereEdgesToSurface = true;
    [SerializeField] private LayerMask adhesionSurfaceMask = ~0;
    [SerializeField] private float adhesionProbeRadius = 0.045f;
    [SerializeField] private float adhesionProbeDistance = 0.16f;
    [SerializeField] private float adhesionSurfaceOffset = 0.002f;
    [SerializeField, Range(0f, 1f)] private float adhesionStartRadius = 0.78f;
    [SerializeField, Range(0f, 1f)] private float edgeAdhesionStrength = 0.88f;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] animatedVertices;
    private readonly Collider[] nearbyColliders = new Collider[16];
    private float maxBaseRadius = 0.001f;
    private float startTime;
    private bool configured;

    public void Configure(float duration, float amplitude, float frequency, float edgeAmplitude)
    {
        spreadDuration = Mathf.Max(0.01f, duration);
        waveAmplitude = Mathf.Max(0f, amplitude);
        waveFrequency = Mathf.Max(0f, frequency);
        edgeFlowAmplitude = Mathf.Max(0f, edgeAmplitude);
        CacheMesh();
        configured = true;
    }

    public void Configure(
        float duration,
        float amplitude,
        float frequency,
        float edgeAmplitude,
        bool useEdgeAdhesion,
        LayerMask surfaceMask,
        float probeRadius,
        float probeDistance,
        float surfaceOffset,
        float startRadius,
        float strength)
    {
        Configure(duration, amplitude, frequency, edgeAmplitude);
        adhereEdgesToSurface = useEdgeAdhesion;
        adhesionSurfaceMask = surfaceMask;
        adhesionProbeRadius = Mathf.Max(0.001f, probeRadius);
        adhesionProbeDistance = Mathf.Max(0.001f, probeDistance);
        adhesionSurfaceOffset = Mathf.Max(0f, surfaceOffset);
        adhesionStartRadius = Mathf.Clamp01(startRadius);
        edgeAdhesionStrength = Mathf.Clamp01(strength);
    }

    private void Awake()
    {
        CacheMesh();
    }

    private void OnEnable()
    {
        startTime = Time.time;
    }

    private void Update()
    {
        if (!configured)
        {
            CacheMesh();
            configured = true;
        }

        if (mesh == null || baseVertices == null || animatedVertices == null)
        {
            return;
        }

        float age = Time.time - startTime;
        float spread01 = Mathf.Clamp01(age / Mathf.Max(0.01f, spreadDuration));
        float spread = Mathf.SmoothStep(0.08f, 1f, spread01);
        float flowFade = 1f - Mathf.Clamp01(age / 2.2f);
        float time = Time.time * waveFrequency;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 vertex = baseVertices[i];
            float radius = new Vector2(vertex.x, vertex.z).magnitude;
            float edge01 = Mathf.Clamp01(radius / maxBaseRadius);
            float angle = Mathf.Atan2(vertex.z, vertex.x);
            float edgeFlow = Mathf.Sin(angle * 5.5f + time) * edgeFlowAmplitude * edge01 * flowFade;
            float adhesionWeight = GetAdhesionWeight(edge01);
            float surfaceLock = adhereEdgesToSurface ? adhesionWeight : 0f;
            float wave = Mathf.Sin(radius * 58f - time * 1.25f + angle * 2.2f) * waveAmplitude * flowFade * (1f - surfaceLock);
            float topBulge = vertex.y * spread * (1f - surfaceLock);
            float radialScale = Mathf.Max(0.02f, spread + edgeFlow);

            Vector3 animatedVertex = new Vector3(
                vertex.x * radialScale,
                topBulge + wave,
                vertex.z * radialScale);

            if (adhereEdgesToSurface && adhesionWeight > 0f)
            {
                animatedVertex = ProjectEdgeVertexToSurface(animatedVertex, adhesionWeight);
            }

            animatedVertices[i] = animatedVertex;
        }

        mesh.vertices = animatedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void CacheMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        mesh = meshFilter.mesh;
        baseVertices = mesh.vertices;
        animatedVertices = new Vector3[baseVertices.Length];
        maxBaseRadius = 0.001f;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            float radius = new Vector2(baseVertices[i].x, baseVertices[i].z).magnitude;
            maxBaseRadius = Mathf.Max(maxBaseRadius, radius);
        }
    }

    private float GetAdhesionWeight(float edge01)
    {
        if (edge01 <= adhesionStartRadius)
        {
            return 0f;
        }

        float range = Mathf.Max(0.001f, 1f - adhesionStartRadius);
        return Mathf.Clamp01((edge01 - adhesionStartRadius) / range) * edgeAdhesionStrength;
    }

    private Vector3 ProjectEdgeVertexToSurface(Vector3 localVertex, float adhesionWeight)
    {
        Vector3 worldVertex = transform.TransformPoint(localVertex);

        if (TryClosestSurfacePoint(worldVertex, out Vector3 projected))
        {
            float lockStrength = Mathf.SmoothStep(0f, 1f, adhesionWeight);
            return transform.InverseTransformPoint(Vector3.Lerp(worldVertex, projected, lockStrength));
        }

        if (TryRayProject(worldVertex, transform.up, -transform.up, out projected))
        {
            return transform.InverseTransformPoint(Vector3.Lerp(worldVertex, projected, adhesionWeight));
        }

        if (TryRayProject(worldVertex, Vector3.up, Vector3.down, out projected))
        {
            return transform.InverseTransformPoint(Vector3.Lerp(worldVertex, projected, adhesionWeight));
        }

        return localVertex;
    }

    private bool TryClosestSurfacePoint(Vector3 worldVertex, out Vector3 projected)
    {
        int count = Physics.OverlapSphereNonAlloc(
            worldVertex,
            adhesionProbeRadius,
            nearbyColliders,
            adhesionSurfaceMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        Vector3 bestPoint = default;
        Vector3 bestNormal = transform.up;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = nearbyColliders[i];
            if (candidate == null || candidate.transform == transform || candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            Vector3 closestPoint = candidate.ClosestPoint(worldVertex);
            float distance = (closestPoint - worldVertex).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestPoint = closestPoint;
            bestNormal = EstimateSurfaceNormal(candidate, closestPoint, worldVertex);
            found = true;
        }

        projected = found ? bestPoint + bestNormal * adhesionSurfaceOffset : Vector3.zero;
        return found;
    }

    private bool TryRayProject(Vector3 worldVertex, Vector3 liftDirection, Vector3 rayDirection, out Vector3 projected)
    {
        Vector3 rayOrigin = worldVertex + liftDirection.normalized * adhesionProbeDistance * 0.5f;
        if (Physics.Raycast(
            rayOrigin,
            rayDirection.normalized,
            out RaycastHit hit,
            adhesionProbeDistance,
            adhesionSurfaceMask,
            QueryTriggerInteraction.Ignore))
        {
            projected = hit.point + hit.normal * adhesionSurfaceOffset;
            return true;
        }

        projected = Vector3.zero;
        return false;
    }

    private Vector3 EstimateSurfaceNormal(Collider collider, Vector3 surfacePoint, Vector3 sourcePoint)
    {
        Vector3 normal = sourcePoint - surfacePoint;
        if (normal.sqrMagnitude > 0.000001f)
        {
            return normal.normalized;
        }

        Vector3 rayOrigin = surfacePoint + transform.up * adhesionProbeDistance * 0.5f;
        if (collider.Raycast(new Ray(rayOrigin, -transform.up), out RaycastHit hit, adhesionProbeDistance))
        {
            return hit.normal;
        }

        return transform.up;
    }
}
