using Seb.Helpers;
using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(LineRenderer))]
public class Simple : MonoBehaviour
{
    public float gravity = 9.8f; // 定义重力大小
    Vector2 position;
    Vector2 velocity;

    [Header("Debug Draw")]
    public int circleSegments = 32;
    public float drawRadius = 0.5f;
    public Color circleColor = Color.red;

    LineRenderer lineRenderer;
    Vector3[] circlePoints;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.loop = true;
        lineRenderer.positionCount = circleSegments;
        lineRenderer.widthMultiplier = 0.05f;
        lineRenderer.useWorldSpace = true;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = circleColor;
        lineRenderer.endColor = circleColor;

        circlePoints = new Vector3[circleSegments];
    }

    void Update()
    {
        velocity += Vector2.down * gravity * Time.deltaTime; // 更新速度，受重力影响
        position += velocity * Time.deltaTime; // 更新位置
        UpdateCircle();
    }

    void UpdateCircle()
    {
        if (circleSegments != circlePoints.Length)
        {
            circlePoints = new Vector3[circleSegments];
            lineRenderer.positionCount = circleSegments;
        }

        for (int i = 0; i < circleSegments; i++)
        {
            float a = 2 * Mathf.PI * i / circleSegments;
            float x = Mathf.Cos(a) * drawRadius;
            float y = Mathf.Sin(a) * drawRadius;
            circlePoints[i] = new Vector3(position.x + x, position.y + y, 0f);
        }

        lineRenderer.SetPositions(circlePoints);
        lineRenderer.startColor = circleColor;
        lineRenderer.endColor = circleColor;
    }
}