using UnityEngine;
//类型文件
public enum PrefabType
{
    Pancake,
    Fruit,
    Topping,
    Cream,
    Syrup
}

[DisallowMultipleComponent]
public class PrefabIdentity : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private PrefabType prefabType = PrefabType.Pancake;

    [Header("Rule Flags")]
    [SerializeField] private bool isJudgeable = true;
    [SerializeField] private bool countsTowardSpawnLimit = true;

    public PrefabType Type => prefabType;
    public bool IsJudgeable => isJudgeable;
    public bool CountsTowardSpawnLimit => countsTowardSpawnLimit;
    public int InstanceId => gameObject.GetInstanceID();

    public static bool TryGetIdentity(Component source, out PrefabIdentity identity)
{
    identity = null;
    if (source == null)
    {
        return false;
    }

    identity = source.GetComponent<PrefabIdentity>();
    if (identity == null)
    {
        identity = source.GetComponentInParent<PrefabIdentity>(true);
    }

    return identity != null;
}

}
