using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class JudgeRequirementEntry
{
    public PrefabType prefabType;
    [Min(0)] public int requiredCount = 1;
}

public class TriggerBoxJudge : MonoBehaviour
{
    [Header("Trigger Box")]
    [SerializeField] private BoxCollider triggerBox;
    [SerializeField] private LayerMask judgeLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Recipe Requirements")]
    [SerializeField] private List<JudgeRequirementEntry> requirements = new List<JudgeRequirementEntry>();
    [SerializeField] private bool rejectUnexpectedTypes = true;

    [Header("Scoring")]
    [SerializeField] [Range(0f, 100f)] private float recipeScoreMax = 70f;
    [SerializeField] [Range(0f, 100f)] private float concentricityScoreMax = 30f;
    [SerializeField] [Min(0f)] private float perfectRadius = 0.02f;
    [SerializeField] [Min(0.001f)] private float zeroScoreRadius = 0.25f;

    [Header("Debug Output")]
    [SerializeField] private bool logJudgeResult = true;
    [SerializeField] private bool judgeWhenChecked;
    [SerializeField] [TextArea(8, 20)] private string lastJudgeSummary;
    [SerializeField] private int lastTotalObjectCount;
    [SerializeField] private bool lastRecipeMatched;
    [SerializeField] private float lastRecipeScore;
    [SerializeField] private float lastConcentricityScore;
    [SerializeField] private float lastTotalScore;
    [SerializeField] private Vector3 lastAnchorPoint;

    public int LastTotalObjectCount => lastTotalObjectCount;
    public bool LastRecipeMatched => lastRecipeMatched;
    public float LastRecipeScore => lastRecipeScore;
    public float LastConcentricityScore => lastConcentricityScore;
    public float LastTotalScore => lastTotalScore;
    public Vector3 LastAnchorPoint => lastAnchorPoint;
    public string LastJudgeSummary => lastJudgeSummary;

    /// <summary>
    /// 当脚本在 Inspector 中被重置时，自动尝试获取当前物体上的 BoxCollider。
    /// </summary>
    private void Reset()
    {
        triggerBox = GetComponent<BoxCollider>();
    }

    private void OnValidate()
    {
        if (!judgeWhenChecked)
        {
            return;
        }

        judgeWhenChecked = false;

        if (!Application.isPlaying)
        {
            lastJudgeSummary = "[TriggerBoxJudge] JudgeWhenChecked only works reliably in Play Mode.";
            return;
        }

        JudgeNow();
    }

    /// <summary>
    /// 核心判定函数：执行当前的评分逻辑。
    /// 包含：扫描物体、构建数量表、查找锚点物体、评估配方准确度、计算同心度得分，并生成总结。
    /// </summary>
    [ContextMenu("Judge Now")]
    public void JudgeNow()
    {
        if (!TryGetTriggerBox(out BoxCollider targetBox))
        {
            lastJudgeSummary = "[TriggerBoxJudge] Missing BoxCollider.";
            if (logJudgeResult)
            {
                Debug.LogWarning(lastJudgeSummary, this);
            }
            return;
        }

        Dictionary<int, JudgeObjectSnapshot> scannedObjects = ScanObjects(targetBox);
        Dictionary<PrefabType, int> actualCounts = BuildCountMap(scannedObjects);

        JudgeObjectSnapshot anchorSnapshot = FindAnchorObject(scannedObjects);
        lastAnchorPoint = anchorSnapshot.HasValue ? anchorSnapshot.Center : Vector3.zero;

        lastTotalObjectCount = scannedObjects.Count;
        lastRecipeMatched = EvaluateRecipe(actualCounts, out int totalDifference, out int totalRequiredCount);
        lastRecipeScore = CalculateRecipeScore(totalDifference, totalRequiredCount);
        lastConcentricityScore = CalculateConcentricityScore(scannedObjects, anchorSnapshot);
        lastTotalScore = lastRecipeScore + lastConcentricityScore;
        lastJudgeSummary = BuildSummary(actualCounts, scannedObjects, anchorSnapshot, totalDifference, totalRequiredCount);

        if (logJudgeResult)
        {
            Debug.Log(lastJudgeSummary, this);
        }
    }

    /// <summary>
    /// 尝试获取有效的 BoxCollider，如果序列化的引用为空，则从当前物体上动态搜索。
    /// </summary>
    private bool TryGetTriggerBox(out BoxCollider targetBox)
    {
        targetBox = triggerBox;
        if (targetBox == null)
        {
            targetBox = GetComponent<BoxCollider>();
        }

        return targetBox != null;
    }

    /// <summary>
    /// 使用 OverlapBox 扫描 TriggerBox 区域内的所有碰撞体。
    /// 过滤掉无效物体，并通过 PrefabIdentity 提取识别到的物体快照映射。
    /// </summary>
    private Dictionary<int, JudgeObjectSnapshot> ScanObjects(BoxCollider targetBox)
    {
        Dictionary<int, JudgeObjectSnapshot> snapshots = new Dictionary<int, JudgeObjectSnapshot>();

        Vector3 center = targetBox.transform.TransformPoint(targetBox.center);
        Vector3 halfExtents = GetScaledHalfExtents(targetBox);
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, targetBox.transform.rotation, judgeLayerMask, queryTriggerInteraction);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hit = colliders[i];
            if (hit == null || hit == targetBox)
            {
                continue;
            }

            if (!PrefabIdentity.TryGetIdentity(hit, out PrefabIdentity identity) || !identity.IsJudgeable)
            {
                continue;
            }

            int instanceId = identity.InstanceId;
            if (snapshots.ContainsKey(instanceId))
            {
                continue;
            }

            snapshots[instanceId] = CreateSnapshot(identity);
        }

        return snapshots;
    }

    /// <summary>
    /// 根据扫描到的快照统计各类型的物体数量。
    /// </summary>
    private Dictionary<PrefabType, int> BuildCountMap(Dictionary<int, JudgeObjectSnapshot> scannedObjects)
    {
        Dictionary<PrefabType, int> counts = new Dictionary<PrefabType, int>();

        foreach (JudgeObjectSnapshot snapshot in scannedObjects.Values)
        {
            if (!counts.ContainsKey(snapshot.Type))
            {
                counts[snapshot.Type] = 0;
            }

            counts[snapshot.Type]++;
        }

        return counts;
    }

    /// <summary>
    /// 查找“锚点物品”（通常是所有物品中位置最靠下的那个，作为对齐基准）。
    /// </summary>
    private JudgeObjectSnapshot FindAnchorObject(Dictionary<int, JudgeObjectSnapshot> scannedObjects)
    {
        bool hasAnchor = false;
        JudgeObjectSnapshot anchorSnapshot = default;

        foreach (JudgeObjectSnapshot snapshot in scannedObjects.Values)
        {
            if (!hasAnchor || snapshot.Center.y < anchorSnapshot.Center.y)
            {
                anchorSnapshot = snapshot;
                hasAnchor = true;
            }
        }

        return anchorSnapshot;
    }

    /// <summary>
    /// 评估实际数量是否符合配方要求。
    /// 会计算差值，并根据 rejectUnexpectedTypes 决定是否扣除多余物体的分数。
    /// </summary>
    private bool EvaluateRecipe(Dictionary<PrefabType, int> actualCounts, out int totalDifference, out int totalRequiredCount)
    {
        totalDifference = 0;
        totalRequiredCount = 0;
        HashSet<PrefabType> requiredTypes = new HashSet<PrefabType>();

        for (int i = 0; i < requirements.Count; i++)
        {
            JudgeRequirementEntry requirement = requirements[i];
            if (requirement == null)
            {
                continue;
            }

            requiredTypes.Add(requirement.prefabType);
            totalRequiredCount += requirement.requiredCount;

            int actualCount = actualCounts.TryGetValue(requirement.prefabType, out int count) ? count : 0;
            totalDifference += Mathf.Abs(actualCount - requirement.requiredCount);
        }

        if (rejectUnexpectedTypes)
        {
            foreach (KeyValuePair<PrefabType, int> pair in actualCounts)
            {
                if (!requiredTypes.Contains(pair.Key))
                {
                    totalDifference += pair.Value;
                }
            }
        }

        return totalDifference == 0;
    }

    /// <summary>
    /// 根据数量准确性计算配方分部分。结果被限制在 0 和 recipeScoreMax 之间。
    /// </summary>
    private float CalculateRecipeScore(int totalDifference, int totalRequiredCount)
    {
        if (recipeScoreMax <= 0f)
        {
            return 0f;
        }

        if (totalRequiredCount <= 0)
        {
            return recipeScoreMax;
        }

        float accuracy = Mathf.Clamp01(1f - (float)totalDifference / totalRequiredCount);
        return accuracy * recipeScoreMax;
    }

    /// <summary>
    /// 计算同心度分部分（叠放对齐程度）。
    /// 将所有物体的水平中心与锚点物体的垂直中心线进行距离计算。
    /// </summary>
    private float CalculateConcentricityScore(Dictionary<int, JudgeObjectSnapshot> scannedObjects, JudgeObjectSnapshot anchorSnapshot)
    {
        if (!anchorSnapshot.HasValue || concentricityScoreMax <= 0f || scannedObjects.Count == 0)
        {
            return 0f;
        }

        float totalScore01 = 0f;
        foreach (JudgeObjectSnapshot snapshot in scannedObjects.Values)
        {
            float distance = GetHorizontalDistanceToVerticalLine(snapshot.Center, anchorSnapshot.Center);
            totalScore01 += CalculateDistanceScore01(distance);
        }

        return (totalScore01 / scannedObjects.Count) * concentricityScoreMax;
    }

    /// <summary>
    ///辅助函数：根据水平距离返回 0 到 1 之间的对齐置信度。 
    /// </summary>
    private float CalculateDistanceScore01(float distance)
    {
        if (distance <= perfectRadius)
        {
            return 1f;
        }

        if (distance >= zeroScoreRadius)
        {
            return 0f;
        }

        return 1f - Mathf.InverseLerp(perfectRadius, zeroScoreRadius, distance);
    }

    /// <summary>
    /// 获取两个三维点在 XZ 平面上的水平距离（忽略高度 Y）。
    /// </summary>
    private static float GetHorizontalDistanceToVerticalLine(Vector3 point, Vector3 lineOrigin)
    {
        Vector2 pointXZ = new Vector2(point.x, point.z);
        Vector2 originXZ = new Vector2(lineOrigin.x, lineOrigin.z);
        return Vector2.Distance(pointXZ, originXZ);
    }

    /// <summary>
    /// 为指定的 PrefabIdentity 创建一个包含其层级包围盒信息的快照。
    /// </summary>
    private JudgeObjectSnapshot CreateSnapshot(PrefabIdentity identity)
    {
        Bounds bounds = CalculateBounds(identity);
        return new JudgeObjectSnapshot(identity.InstanceId, identity.Type, bounds.center, bounds);
    }

    /// <summary>
    /// 计算一个物体及其所有子层级中所有碰撞体的合并包围盒。
    /// </summary>
    private static Bounds CalculateBounds(PrefabIdentity identity)
    {
        Collider[] colliders = identity.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            return new Bounds(identity.transform.position, Vector3.zero);
        }

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            bounds.Encapsulate(colliders[i].bounds);
        }

        return bounds;
    }

    /// <summary>
    /// 将 BoxCollider 的本地大小转换为世界空间下受缩放影响的半长度（HalfExtents）。
    /// </summary>
    private static Vector3 GetScaledHalfExtents(BoxCollider box)
    {
        Vector3 scaledSize = Vector3.Scale(box.size, box.transform.lossyScale);
        return new Vector3(Mathf.Abs(scaledSize.x), Mathf.Abs(scaledSize.y), Mathf.Abs(scaledSize.z)) * 0.5f;
    }

    /// <summary>
    /// 构建并生成最终的可读总结文本，用于调试或 UI 显示。
    /// </summary>
    private string BuildSummary(
        Dictionary<PrefabType, int> actualCounts,
        Dictionary<int, JudgeObjectSnapshot> scannedObjects,
        JudgeObjectSnapshot anchorSnapshot,
        int totalDifference,
        int totalRequiredCount)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[TriggerBoxJudge] Judge completed.");
        builder.AppendLine($"Objects in box: {scannedObjects.Count}");
        builder.AppendLine($"Recipe matched: {lastRecipeMatched}");
        builder.AppendLine($"Recipe score: {lastRecipeScore:F2}/{recipeScoreMax:F2}");
        builder.AppendLine($"Concentricity score: {lastConcentricityScore:F2}/{concentricityScoreMax:F2}");
        builder.AppendLine($"Total score: {lastTotalScore:F2}/{recipeScoreMax + concentricityScoreMax:F2}");
        builder.AppendLine($"Recipe difference: {totalDifference} (required total: {totalRequiredCount})");

        if (anchorSnapshot.HasValue)
        {
            builder.AppendLine($"Anchor object: {anchorSnapshot.Type} at {anchorSnapshot.Center}");
        }
        else
        {
            builder.AppendLine("Anchor object: none");
        }

        builder.AppendLine("Requirements:");
        if (requirements.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                JudgeRequirementEntry requirement = requirements[i];
                if (requirement == null)
                {
                    continue;
                }

                int actualCount = actualCounts.TryGetValue(requirement.prefabType, out int count) ? count : 0;
                builder.AppendLine($"- {requirement.prefabType}: actual {actualCount}, required {requirement.requiredCount}");
            }
        }

        builder.AppendLine("Actual counts:");
        if (actualCounts.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (KeyValuePair<PrefabType, int> pair in actualCounts)
            {
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            }
        }

        return builder.ToString();
    }

    private readonly struct JudgeObjectSnapshot
    {
        public readonly int InstanceId;
        public readonly PrefabType Type;
        public readonly Vector3 Center;
        public readonly Bounds Bounds;

        public bool HasValue => InstanceId != 0;

        public JudgeObjectSnapshot(int instanceId, PrefabType type, Vector3 center, Bounds bounds)
        {
            InstanceId = instanceId;
            Type = type;
            Center = center;
            Bounds = bounds;
        }
    }
}
