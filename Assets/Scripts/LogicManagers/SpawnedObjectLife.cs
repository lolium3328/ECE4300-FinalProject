using UnityEngine;

[DisallowMultipleComponent]
public class SpawnedObjectLife : MonoBehaviour
{
    private void OnDestroy()
    {
        if (SpawnLimitManager.Instance == null)
        {
            return;
        }

        if (!PrefabIdentity.TryGetIdentity(transform, out PrefabIdentity identity))
        {
            return;
        }

        if (!identity.CountsTowardSpawnLimit)
        {
            return;
        }

        if (!SpawnLimitManager.Instance.IsRegistered(gameObject))
        {
            return;
        }

        SpawnLimitManager.Instance.UnregisterSpawn(identity);
    }
}
