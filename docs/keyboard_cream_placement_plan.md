# 键盘放置奶油实现方案

## 目标

先做一个最简单的键盘调试版本：

- 在 topping 阶段按一个键选择奶油。
- 再按生成键，把一团奶油放到当前生成点。
- 奶油视觉上由很多个小球组成。
- 小球本身没有碰撞体，也不单独参与 judge。
- 奶油父物体作为一个 `PrefabType.Cream` 被 `TriggerBoxJudge` 统计。
- 后续可以让 `TriggerBoxJudge` 根据小球平均中心点和离散程度来评分。

## 会用到的现有系统

### `InputManager`

负责读键盘输入。

当前已经有类似逻辑：

```text
W -> 在手势模式下选择草莓
S -> 在当前位置生成当前 prefab
```

奶油可以沿用这个模式。

### `HandSpawnController`

负责当前生成点，以及真正生成 prefab。

第一版不需要重写生成逻辑，继续用现有的：

```csharp
handSpawnController.SpawnAtCurrentPoint();
```

### `GestureSpawnSelector`

负责把一个 label 映射成 prefab。

例如现在大概是：

```text
label "0" -> pancake
label "1" -> strawberry
label "C" -> none
```

奶油可以新增：

```text
label "2" -> CreamCluster.prefab
```

### `PrefabIdentity`

`TriggerBoxJudge` 会通过这个组件识别物体类型。

奶油父物体要挂：

```text
PrefabIdentity
prefabType = Cream
isJudgeable = true
```

### `TriggerBoxJudge`

负责最后扫描触发盒里的物体并评分。

第一版里，`TriggerBoxJudge` 只需要把 `CreamCluster` 当成一个 `Cream` 来数。

## 需要新增的文件

### `Assets/Scripts/GadingManager/CreamBlobGroup.cs`

作用：

- 挂在奶油父物体 `CreamCluster` 上。
- 保存所有小球 blob 的 Transform。
- 后续给 judge 提供平均中心点和离散程度。

建议内容：

```csharp
using UnityEngine;

public class CreamBlobGroup : MonoBehaviour
{
    [SerializeField] private Transform[] blobs;

    public bool TryGetAverageCenter(out Vector3 center)
    {
        center = Vector3.zero;

        if (blobs == null || blobs.Length == 0)
        {
            return false;
        }

        int count = 0;
        foreach (Transform blob in blobs)
        {
            if (blob == null)
            {
                continue;
            }

            center += blob.position;
            count++;
        }

        if (count == 0)
        {
            return false;
        }

        center /= count;
        return true;
    }

    public float GetAverageSpreadRadius(Vector3 center)
    {
        if (blobs == null || blobs.Length == 0)
        {
            return 0f;
        }

        float totalDistance = 0f;
        int count = 0;

        foreach (Transform blob in blobs)
        {
            if (blob == null)
            {
                continue;
            }

            Vector2 blobXZ = new Vector2(blob.position.x, blob.position.z);
            Vector2 centerXZ = new Vector2(center.x, center.z);
            totalDistance += Vector2.Distance(blobXZ, centerXZ);
            count++;
        }

        return count > 0 ? totalDistance / count : 0f;
    }
}
```

第一版可以先只加这个脚本，不马上改 judge。

## 需要新增的 prefab

### `Assets/Prefabs/Cream/CreamBlob.prefab`

结构：

```text
CreamBlob
├── MeshFilter
├── MeshRenderer
└── 不要 Collider
```

设置：

- Mesh 用 Sphere 或低模球。
- 不挂 `Collider`。
- 不挂 `Rigidbody`。
- 不挂 `PrefabIdentity`。
- 材质统一使用奶油材质。

这个 prefab 只负责视觉。

### `Assets/Prefabs/Cream/CreamCluster.prefab`

结构：

```text
CreamCluster
├── PrefabIdentity
├── CreamBlobGroup
├── Blob_01
├── Blob_02
├── Blob_03
├── Blob_04
└── ...
```

父物体设置：

```text
PrefabIdentity.prefabType = Cream
PrefabIdentity.isJudgeable = true
PrefabIdentity.countsTowardSpawnLimit = true
```

子物体设置：

```text
Blob_01 / Blob_02 / ...
只放 MeshRenderer + MeshFilter
不要 Collider
不要 PrefabIdentity
```

第一版推荐做 6 到 12 个小球，互相重叠：

```text
CreamCluster
├── Blob_01 scale 0.08
├── Blob_02 scale 0.06
├── Blob_03 scale 0.07
├── Blob_04 scale 0.05
├── Blob_05 scale 0.06
└── Blob_06 scale 0.04
```

小球位置可以手动摆成一团，不需要一开始就写自动生成。

## 键盘输入设计

推荐先用：

```text
E -> 选择奶油 prefab
S -> 在当前位置生成当前 prefab
```

在 `InputManager.Update()` 里加：

```csharp
if (Input.GetKeyDown(KeyCode.E) && ProcessManager.Instance.IsGestureMode())
{
    gestureSpawnSelector.ApplyRecognizedLabel("2");
}
```

然后在 `GestureSpawnSelector` 的 Inspector 里新增 mapping：

```text
gestureLabel: 2
prefab: CreamCluster.prefab
```

这样就和当前 `W -> 草莓` 的逻辑一致。

## 场景里要怎么接

在 `MainScene`：

1. 选中 `GestureSpawnController`。
2. 找到 `GestureSpawnSelector`。
3. 在 `mappings` 里新增一项：

```text
gestureLabel = 2
prefab = Assets/Prefabs/Cream/CreamCluster.prefab
```

4. 选中 `InputManager`。
5. 确认引用已经拖好：

```text
handSpawnController
gestureSpawnSelector
```

6. 选中 `TriggerBox`。
7. 确认 `TriggerBox` 的范围包住盘子/松饼区域。

## 第一版 judge 逻辑

第一版不用改 `TriggerBoxJudge`。

行为是：

```text
CreamCluster 父物体算作一个 Cream
Blob 子物体只是视觉
recipe 数量判断正常工作
同心度评分暂时使用 CreamCluster 的 bounds.center
```

只要 `CreamCluster` 有一个能被扫描到的 collider，`TriggerBoxJudge` 就能扫到它。

注意：

- 如果父物体完全没有 collider，`Physics.OverlapBox` 扫不到它。
- 子球不要加 collider，否则可能让调试变复杂。
- 推荐父物体加一个很小的 `SphereCollider` 或 `BoxCollider`，只用于 judge 扫描。

## 后续增强：用小球平均中心点评分

之后可以改 `TriggerBoxJudge.CreateSnapshot()`：

```csharp
private JudgeObjectSnapshot CreateSnapshot(PrefabIdentity identity)
{
    Bounds bounds = CalculateBounds(identity);
    Vector3 center = bounds.center;

    CreamBlobGroup creamGroup = identity.GetComponent<CreamBlobGroup>();
    if (creamGroup != null && creamGroup.TryGetAverageCenter(out Vector3 creamCenter))
    {
        center = creamCenter;
    }

    return new JudgeObjectSnapshot(identity.InstanceId, identity.Type, center, bounds);
}
```

这样：

```text
普通物体 -> 用 bounds.center
CreamCluster -> 用所有 blob 的平均中心点
```

再往后可以把 `GetAverageSpreadRadius()` 也接进评分，计算奶油小球是否太散。

## 最小实现顺序

1. 新建 `CreamBlobGroup.cs`。
2. 新建奶油材质。
3. 新建 `CreamBlob.prefab`。
4. 新建 `CreamCluster.prefab`。
5. 给 `CreamCluster` 加 `PrefabIdentity`。
6. 给 `CreamCluster` 加 `CreamBlobGroup`。
7. 给 `CreamCluster` 父物体加一个小 collider。
8. 在 `GestureSpawnSelector` 里加 `label "2" -> CreamCluster.prefab`。
9. 在 `InputManager` 里加 `E -> ApplyRecognizedLabel("2")`。
10. 进入 topping 阶段，按 `E`，再按 `S`。
11. 用 `TriggerBoxJudge` 确认能扫到一个 `Cream`。

