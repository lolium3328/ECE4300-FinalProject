using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class JamFluidSurfaceAnimator : MonoBehaviour
{
    [SerializeField] private float spreadDuration = 0.65f;
    [SerializeField] private float waveAmplitude = 0.0035f;
    [SerializeField] private float waveFrequency = 3.2f;
    [SerializeField] private float edgeFlowAmplitude = 0.055f;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] animatedVertices;
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
            float wave = Mathf.Sin(radius * 58f - time * 1.25f + angle * 2.2f) * waveAmplitude * flowFade;
            float radialScale = Mathf.Max(0.02f, spread + edgeFlow);

            animatedVertices[i] = new Vector3(
                vertex.x * radialScale,
                vertex.y * spread + wave,
                vertex.z * radialScale);
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
}
