using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PrefabSpawnLimitEntry
{
    public PrefabType prefabType;
    [Min(-1)] public int maxCount = -1;
}

public class SpawnLimitManager : MonoBehaviour
{
    public static SpawnLimitManager Instance { get; private set; }

    [Header("Spawn Limits")]
    [SerializeField] private int defaultMaxCount = -1;
    [SerializeField] private List<PrefabSpawnLimitEntry> spawnLimits = new List<PrefabSpawnLimitEntry>();

    private readonly Dictionary<PrefabType, int> maxCountByType = new Dictionary<PrefabType, int>();
    private readonly Dictionary<PrefabType, HashSet<int>> aliveInstanceIdsByType = new Dictionary<PrefabType, HashSet<int>>();
    private readonly Dictionary<int, PrefabType> typeByInstanceId = new Dictionary<int, PrefabType>();

    /// <summary>
    /// 重建生成限制的缓存。初始化类型字典并应用自定义限制。
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        RebuildLimitCache();
    }

    /// <summary>
    /// 在编辑器中修改属性时触发，用于实时更新生成限制配置。
    /// </summary>
    private void OnValidate()
    {
        RebuildLimitCache();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 检查指定类型的预制体是否可以生成。
    /// </summary>
    /// <param name="type">预制体类型</param>
    /// <returns>如果当前存活数量小于最大限制或无限制（-1），则返回 true</returns>
    public bool CanSpawn(PrefabType type)
    {
        int maxCount = GetMaxCount(type);
        if (maxCount < 0)
        {
            return true;
        }

        return GetAliveCount(type) < maxCount;
    }

    /// <summary>
    /// 根据 PrefabIdentity 检查是否可以生成。
    /// </summary>
    /// <param name="identity">预制体标识组件</param>
    /// <returns>如果 identity 无效或不受生成限制，则返回 true；否则检查生成限制</returns>
    public bool CanSpawn(PrefabIdentity identity)
    {
        if (identity == null)
        {
            return false;
        }

        if (!identity.CountsTowardSpawnLimit)
        {
            return true;
        }

        return CanSpawn(identity.Type);
    }

    /// <summary>
    /// 注册生成的 GameObject。
    /// </summary>
    /// <param name="instance">生成的实例物体</param>
    /// <returns>注册成功返回 true</returns>
    public bool RegisterSpawn(GameObject instance)
    {
        if (!PrefabIdentity.TryGetIdentity(instance != null ? instance.transform : null, out PrefabIdentity identity))
        {
            return false;
        }

        return RegisterSpawn(identity);
    }

    /// <summary>
    /// 注册生成的 PrefabIdentity。
    /// </summary>
    /// <param name="identity">预制体标识组件</param>
    /// <returns>注册成功或无需限制返回 true，若超过上限则返回 false</returns>
    public bool RegisterSpawn(PrefabIdentity identity)
    {
        if (identity == null)
        {
            return false;
        }

        if (!identity.CountsTowardSpawnLimit)
        {
            return true;
        }

        int instanceId = identity.InstanceId;
        if (typeByInstanceId.ContainsKey(instanceId))
        {
            return true;
        }

        if (!CanSpawn(identity.Type))
        {
            return false;
        }

        aliveInstanceIdsByType[identity.Type].Add(instanceId);
        typeByInstanceId[instanceId] = identity.Type;
        return true;
    }

    /// <summary>
    /// 注销（物体销毁时）注册的 GameObject。
    /// </summary>
    /// <param name="instance">销毁的实例物体</param>
    /// <returns>注销成功返回 true</returns>
    public bool UnregisterSpawn(GameObject instance)
    {
        if (!PrefabIdentity.TryGetIdentity(instance != null ? instance.transform : null, out PrefabIdentity identity))
        {
            return false;
        }

        return UnregisterSpawn(identity);
    }

    /// <summary>
    /// 注销（物体销毁时）注册的 PrefabIdentity。
    /// </summary>
    /// <param name="identity">预制体标识组件</param>
    /// <returns>注销成功返回 true</returns>
    public bool UnregisterSpawn(PrefabIdentity identity)
    {
        if (identity == null)
        {
            return false;
        }

        if (!identity.CountsTowardSpawnLimit)
        {
            return true;
        }

        int instanceId = identity.InstanceId;
        if (!typeByInstanceId.TryGetValue(instanceId, out PrefabType type))
        {
            return false;
        }

        typeByInstanceId.Remove(instanceId);
        aliveInstanceIdsByType[type].Remove(instanceId);
        return true;
    }

    /// <summary>
    /// 检查指定物体是否已被注册追踪。
    /// </summary>
    public bool IsRegistered(GameObject instance)
    {
        if (!PrefabIdentity.TryGetIdentity(instance != null ? instance.transform : null, out PrefabIdentity identity))
        {
            return false;
        }

        return typeByInstanceId.ContainsKey(identity.InstanceId);
    }

    /// <summary>
    /// 获取指定类型当前存活的实例数量。
    /// </summary>
    public int GetAliveCount(PrefabType type)
    {
        return aliveInstanceIdsByType.TryGetValue(type, out HashSet<int> instanceIds) ? instanceIds.Count : 0;
    }

    /// <summary>
    /// 获取指定类型允许的最大生成数量。
    /// </summary>
    public int GetMaxCount(PrefabType type)
    {
        return maxCountByType.TryGetValue(type, out int maxCount) ? maxCount : defaultMaxCount;
    }

    /// <summary>
    /// 获取指定类型剩余可生成的数量。
    /// </summary>
    /// <returns>剩余数量，如果无限制则返回 -1</returns>
    public int GetRemainingSpawnCount(PrefabType type)
    {
        int maxCount = GetMaxCount(type);
        if (maxCount < 0)
        {
            return -1;
        }

        return Mathf.Max(0, maxCount - GetAliveCount(type));
    }

    /// <summary>
    /// 清除所有当前追踪的实例数据。
    /// </summary>
    public void ClearAllTracking()
    {
        typeByInstanceId.Clear();

        foreach (HashSet<int> instanceIds in aliveInstanceIdsByType.Values)
        {
            instanceIds.Clear();
        }
    }

    /// <summary>
    /// 内部方法：根据配置重新构建生成限制映射。
    /// </summary>
    private void RebuildLimitCache()
    {
        maxCountByType.Clear();

        foreach (PrefabType type in Enum.GetValues(typeof(PrefabType)))
        {
            if (!aliveInstanceIdsByType.ContainsKey(type))
            {
                aliveInstanceIdsByType[type] = new HashSet<int>();
            }

            maxCountByType[type] = defaultMaxCount;
        }

        foreach (PrefabSpawnLimitEntry entry in spawnLimits)
        {
            if (entry == null)
            {
                continue;
            }

            maxCountByType[entry.prefabType] = entry.maxCount;
        }
    }
}
