using System.Collections.Generic;
using UnityEngine;

public class CreamSphereCluster : MonoBehaviour
{
    private enum ClusterShape
    {
        FluidJam,
        CloudJam,
        Scatter
    }

    [Header("Sphere Distribution")]
    [SerializeField] private ClusterShape shape = ClusterShape.FluidJam;
    [SerializeField] private int sphereCount = 26;
    [SerializeField] private float clusterRadius = 0.095f;
    [SerializeField] private float centerBias = 2.8f;
    [SerializeField] private float sphereScale = 0.032f;
    [SerializeField] private float scaleJitter = 0.009f;
    [SerializeField] private float heightJitter = 0.012f;
    [SerializeField] private float yScaleMultiplier = 0.38f;

    [Header("Cloud Jam Shape")]
    [SerializeField] private int coreBlobCount = 7;
    [SerializeField] private float coreRadius = 0.045f;
    [SerializeField] private float coreScaleMultiplier = 1.35f;
    [SerializeField] private float edgeDropletChance = 0.22f;
    [SerializeField] private float edgeDropletScaleMultiplier = 0.72f;

    [Header("Fluid Jam Mesh")]
    [SerializeField] private int fluidSegments = 72;
    [SerializeField] private int fluidRings = 6;
    [SerializeField] private float fluidThickness = 0.005f;
    [SerializeField] private float edgeIrregularity = 0.18f;
    [SerializeField] private int dripCount = 4;
    [SerializeField] private float dripLength = 0.04f;
    [SerializeField, Range(0f, 1f)] private float gravityDripBias = 0.65f;
    [SerializeField] private float spreadDuration = 0.65f;
    [SerializeField] private float waveAmplitude = 0.0035f;
    [SerializeField] private float waveFrequency = 3.2f;
    [SerializeField] private float edgeFlowAmplitude = 0.055f;

    [Header("Surface Adhesion")]
    [SerializeField] private bool adhereEdgesToSurface = true;
    [SerializeField] private LayerMask adhesionSurfaceMask = ~0;
    [SerializeField] private float adhesionProbeRadius = 0.045f;
    [SerializeField] private float adhesionProbeDistance = 0.16f;
    [SerializeField] private float adhesionSurfaceOffset = 0.002f;
    [SerializeField, Range(0f, 1f)] private float adhesionStartRadius = 0.78f;
    [SerializeField, Range(0f, 1f)] private float edgeAdhesionStrength = 0.88f;

    [Header("Material")]
    [SerializeField] private Material sphereMaterial;
    [SerializeField] private Color jamColor = new Color(0.72f, 0.06f, 0.13f, 1f);
    [SerializeField] private float smoothness = 0.82f;

    [Header("Behavior")]
    [SerializeField] private bool generateOnAwake = true;

    private readonly List<GameObject> generatedSpheres = new List<GameObject>();
    private Material runtimeMaterial;

    private void Awake()
    {
        if (generateOnAwake)
        {
            Generate();
        }
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        // Clear();

        if (shape == ClusterShape.FluidJam)
        {
            GenerateFluidSurface();
            return;
        }

        int count = Mathf.Max(0, sphereCount);
        for (int i = 0; i < count; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"CreamSphere_{i + 1:00}";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = SampleLocalPosition(i, count);
            sphere.transform.localRotation = Random.rotationUniform;
            sphere.transform.localScale = SampleLocalScale(i, count, sphere.transform.localPosition);

            Collider sphereCollider = sphere.GetComponent<Collider>();
            if (sphereCollider != null)
            {
                Destroy(sphereCollider);
            }

            MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetMaterial();
            }

            generatedSpheres.Add(sphere);
        }
    }

    // [ContextMenu("Clear")]
    // public void Clear()     //销毁生成的球体和流体表面
    // {
    //     Debug.Log("Clearing CreamSphereCluster...");
        
    //     // 销毁已记录的生成对象
    //     for (int i = generatedSpheres.Count - 1; i >= 0; i--)
    //     {
    //         if (generatedSpheres[i] != null)
    //         {
    //             DestroyGeneratedObject(generatedSpheres[i]);
    //             Debug.Log($"Destroyed generated object: {generatedSpheres[i].name}");
    //         }
    //     }
    //     generatedSpheres.Clear();

    //     // 销毁所有匹配名称的子对象（处理可能遗漏的对象）
    //     // 重要：先收集所有要销毁的对象到列表中，再销毁
    //     // 这样避免在销毁时childCount改变导致索引混乱
    //     List<GameObject> childrenToDestroy = new List<GameObject>();
    //     for (int i = 0; i < transform.childCount; i++)
    //     {
    //         Transform child = transform.GetChild(i);
    //         if (child != null && (child.name.StartsWith("CreamSphere_") || child.name.StartsWith("CreamFluid_")))
    //         {
    //             childrenToDestroy.Add(child.gameObject);
    //         }
    //     }

    //     // 现在销毁收集到的对象
    //     foreach (GameObject child in childrenToDestroy)
    //     {
    //         if (child != null)
    //         {
    //             DestroyGeneratedObject(child);
    //             Debug.Log($"Destroyed child object: {child.name}");
    //         }
    //     }
    // }

    private void GenerateFluidSurface()
    {
        GameObject fluid = new GameObject("CreamFluid_Surface");
        fluid.transform.SetParent(transform, false);
        fluid.transform.localPosition = Vector3.up * fluidThickness;
        fluid.transform.localRotation = Quaternion.identity;
        fluid.transform.localScale = Vector3.one;

        MeshFilter meshFilter = fluid.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = fluid.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = CreateFluidMesh();
        meshRenderer.sharedMaterial = GetMaterial();

        JamFluidSurfaceAnimator animator = fluid.AddComponent<JamFluidSurfaceAnimator>();
        animator.Configure(
            spreadDuration,
            waveAmplitude,
            waveFrequency,
            edgeFlowAmplitude,
            adhereEdgesToSurface,
            adhesionSurfaceMask,
            adhesionProbeRadius,
            adhesionProbeDistance,
            adhesionSurfaceOffset,
            adhesionStartRadius,
            edgeAdhesionStrength);

        generatedSpheres.Add(fluid);
    }

    private Mesh CreateFluidMesh()
    {
        int segments = Mathf.Clamp(fluidSegments, 12, 160);
        int rings = Mathf.Clamp(fluidRings, 2, 24);
        float[] boundaryRadii = BuildFluidBoundaryRadii(segments);

        int topVertexCount = 1 + segments * rings;
        List<Vector3> vertices = new List<Vector3>(topVertexCount + segments);
        List<Vector2> uvs = new List<Vector2>(topVertexCount + segments);
        List<int> triangles = new List<int>(segments * ((rings - 1) * 6 + 9));

        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int ring = 1; ring <= rings; ring++)
        {
            float ring01 = ring / (float)rings;
            float easedRing = Mathf.Pow(ring01, 0.82f);

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = Mathf.PI * 2f * segment / segments;
                float radius = boundaryRadii[segment] * easedRing;
                float surfaceBulge = Mathf.Sin(ring01 * Mathf.PI) * fluidThickness;
                Vector3 vertex = new Vector3(
                    Mathf.Cos(angle) * radius,
                    surfaceBulge,
                    Mathf.Sin(angle) * radius);

                vertices.Add(vertex);
                uvs.Add(new Vector2(vertex.x / (clusterRadius * 2f) + 0.5f, vertex.z / (clusterRadius * 2f) + 0.5f));
            }
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(0);
            triangles.Add(1 + next);
            triangles.Add(1 + segment);
        }

        for (int ring = 2; ring <= rings; ring++)
        {
            int innerStart = 1 + (ring - 2) * segments;
            int outerStart = 1 + (ring - 1) * segments;

            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                int inner = innerStart + segment;
                int innerNext = innerStart + next;
                int outer = outerStart + segment;
                int outerNext = outerStart + next;

                triangles.Add(inner);
                triangles.Add(innerNext);
                triangles.Add(outerNext);

                triangles.Add(inner);
                triangles.Add(outerNext);
                triangles.Add(outer);
            }
        }

        int outerTopStart = 1 + (rings - 1) * segments;
        int outerBottomStart = vertices.Count;
        float sideBottomY = -Mathf.Max(0.001f, fluidThickness * 0.75f);

        for (int segment = 0; segment < segments; segment++)
        {
            Vector3 topVertex = vertices[outerTopStart + segment];
            vertices.Add(new Vector3(topVertex.x, sideBottomY, topVertex.z));
            uvs.Add(uvs[outerTopStart + segment]);
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            int top = outerTopStart + segment;
            int topNext = outerTopStart + next;
            int bottom = outerBottomStart + segment;
            int bottomNext = outerBottomStart + next;

            triangles.Add(top);
            triangles.Add(topNext);
            triangles.Add(bottomNext);

            triangles.Add(top);
            triangles.Add(bottomNext);
            triangles.Add(bottom);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Runtime_CreamFluidMesh";
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private float[] BuildFluidBoundaryRadii(int segments)
    {
        float[] radii = new float[segments];
        float seed = Random.Range(0f, 1000f);
        float baseRadius = Mathf.Max(0.01f, clusterRadius);
        int safeDripCount = Mathf.Clamp(dripCount, 0, 12);
        float[] dripAngles = new float[safeDripCount];
        float gravityFlowAngle = GetLocalGravityFlowAngle();

        for (int i = 0; i < safeDripCount; i++)
        {
            dripAngles[i] = Random.value < gravityDripBias
                ? gravityFlowAngle + Random.Range(-0.5f, 0.5f)
                : Random.Range(0f, Mathf.PI * 2f);
        }

        for (int segment = 0; segment < segments; segment++)
        {
            float angle = Mathf.PI * 2f * segment / segments;
            float noiseA = Mathf.PerlinNoise(seed + Mathf.Cos(angle) * 1.7f, seed + Mathf.Sin(angle) * 1.7f);
            float noiseB = Mathf.PerlinNoise(seed * 0.37f + Mathf.Cos(angle) * 4.5f, seed * 0.37f + Mathf.Sin(angle) * 4.5f);
            float radius = baseRadius * (1f + (noiseA - 0.5f) * edgeIrregularity + (noiseB - 0.5f) * edgeIrregularity * 0.55f);

            for (int drip = 0; drip < safeDripCount; drip++)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, dripAngles[drip] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                float influence = Mathf.Clamp01(1f - delta / 0.28f);
                radius += dripLength * influence * influence;
            }

            radii[segment] = Mathf.Max(baseRadius * 0.55f, radius);
        }

        return radii;
    }

    private float GetLocalGravityFlowAngle()
    {
        Vector3 localGravity = transform.InverseTransformDirection(Vector3.down);
        localGravity.y = 0f;

        if (localGravity.sqrMagnitude < 0.0001f)
        {
            localGravity = Vector3.back;
        }

        localGravity.Normalize();
        return Mathf.Atan2(localGravity.z, localGravity.x);
    }

    private Vector3 SampleLocalPosition(int index, int totalCount)
    {
        if (shape == ClusterShape.Scatter)
        {
            return SampleScatterPosition();
        }

        int safeCoreCount = Mathf.Clamp(coreBlobCount, 0, totalCount);
        if (index < safeCoreCount)
        {
            return SampleCorePosition(index, safeCoreCount);
        }

        return SampleCloudEdgePosition();
    }

    private Vector3 SampleScatterPosition()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float safeBias = Mathf.Max(0.01f, centerBias);
        float radius01 = Mathf.Pow(Random.value, safeBias);
        float radius = radius01 * Mathf.Max(0f, clusterRadius);
        float y = Random.Range(0f, Mathf.Max(0f, heightJitter));

        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    private Vector3 SampleCorePosition(int index, int coreCount)
    {
        if (index == 0 || coreCount <= 1)
        {
            return new Vector3(0f, heightJitter * 0.35f, 0f);
        }

        float angle = (Mathf.PI * 2f * index / coreCount) + Random.Range(-0.35f, 0.35f);
        float radius = Random.Range(coreRadius * 0.35f, coreRadius);
        float y = Random.Range(0f, Mathf.Max(0f, heightJitter));

        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    private Vector3 SampleCloudEdgePosition()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float safeBias = Mathf.Max(0.01f, centerBias);
        float radius01 = Mathf.Pow(Random.value, 1f / safeBias);
        float radius = radius01 * Mathf.Max(0f, clusterRadius);
        float y = Random.Range(0f, Mathf.Max(0f, heightJitter));

        if (Random.value < edgeDropletChance)
        {
            radius = Random.Range(clusterRadius * 0.82f, clusterRadius * 1.18f);
            y *= 0.45f;
        }

        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    private Vector3 SampleLocalScale(int index, int totalCount, Vector3 localPosition)
    {
        float randomOffset = Random.Range(-scaleJitter, scaleJitter);
        float scale = Mathf.Max(0.001f, sphereScale + randomOffset);
        float radius01 = clusterRadius > 0f
            ? Mathf.Clamp01(new Vector2(localPosition.x, localPosition.z).magnitude / clusterRadius)
            : 0f;

        if (shape == ClusterShape.CloudJam)
        {
            int safeCoreCount = Mathf.Clamp(coreBlobCount, 0, totalCount);
            if (index < safeCoreCount)
            {
                scale *= coreScaleMultiplier;
            }
            else if (radius01 > 0.82f)
            {
                scale *= edgeDropletScaleMultiplier;
            }
            else
            {
                scale *= Mathf.Lerp(1.15f, 0.86f, radius01);
            }
        }

        float xScale = scale * Random.Range(1.05f, 1.55f);
        float zScale = scale * Random.Range(0.95f, 1.45f);
        float yScale = scale * Mathf.Max(0.001f, yScaleMultiplier) * Random.Range(0.82f, 1.18f);

        return new Vector3(xScale, yScale, zScale);
    }

    private Material GetMaterial()
    {
        if (sphereMaterial != null)
        {
            return sphereMaterial;
        }

        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        runtimeMaterial = new Material(shader);
        runtimeMaterial.name = "Runtime_JamGloss";

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", jamColor);
        }

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", jamColor);
        }

        if (runtimeMaterial.HasProperty("_Smoothness"))
        {
            runtimeMaterial.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        }

        if (runtimeMaterial.HasProperty("_Metallic"))
        {
            runtimeMaterial.SetFloat("_Metallic", 0f);
        }

        ConfigureTransparentMaterial(runtimeMaterial);

        return runtimeMaterial;
    }

    private void ConfigureTransparentMaterial(Material material)
    {
        if (material == null || jamColor.a >= 0.995f)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private static void DestroyGeneratedObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
