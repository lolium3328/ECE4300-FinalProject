# TriggerBox 中 prefab 判定与最大生成数量控制逻辑

## 1. 目标

这个“裁判逻辑”需要解决两件事：

1. 判断一个 `TriggerBox` 里是否已经出现了指定的几种 `prefab`。
2. 限制场上每一种 `prefab` 的最大生成数量，避免无限生成。

结合你们当前项目结构，更合理的做法不是把所有逻辑都塞进 `HandSpawnController`，而是拆成两个模块：

- `SpawnLimitManager`：负责“能不能生成”和“当前场上数量是多少”。
- `TriggerBoxJudge`：负责“盒子里现在有哪些 prefab”和“是否满足组合条件”。

这样职责清楚，也方便以后扩展到不同步骤、不同配方、不同判定区。

---

## 2. 推荐的脚本职责划分

### 2.1 `PrefabIdentity`

给每个可生成物体挂一个轻量脚本，用来标记它到底属于哪一种类型。

推荐字段：

```csharp
public enum PrefabType
{
    Pancake,
    Fruit,
    Topping,
    Cream,
    Syrup，
    
}

public class PrefabIdentity : MonoBehaviour
{
    public PrefabType prefabType;
}
```

作用：

- 不要靠 `gameObject.name` 判断类型，因为实例化后名字可能变成 `xxx(Clone)`。
- 不要只靠 `tag`，因为 `tag` 粒度太粗，后面类型一多会不够用。
- 最稳定的方式是给 prefab 显式挂一个身份脚本。

---

### 2.2 `SpawnLimitManager`

这个脚本负责全局统计“场上每种 prefab 当前有多少个实例”。

它应该提供这几个核心接口：

```csharp
bool CanSpawn(PrefabType type)
void RegisterSpawn(PrefabType type, GameObject instance)
void UnregisterSpawn(PrefabType type, GameObject instance)
int GetAliveCount(PrefabType type)
```

建议内部使用：

```csharp
Dictionary<PrefabType, int> aliveCountByType;
Dictionary<PrefabType, int> maxCountByType;
```

逻辑是：

1. 生成前先调用 `CanSpawn(type)`。
2. 如果当前数量 `< 最大数量`，允许生成。
3. 生成成功后立刻调用 `RegisterSpawn(type, instance)`。
4. 物体销毁、被回收、被提交进判定区后，再调用 `UnregisterSpawn(type, instance)`。

这部分控制的是“场上总数”，不是“TriggerBox 里有多少个”。

---

### 2.3 `TriggerBoxJudge`

这个脚本挂在带 `isTrigger = true` 的判定盒上，负责统计盒子里面有哪些 prefab。

它至少要回答两个问题：

1. 盒子里是否有某几种 prefab。
2. 每种 prefab 在盒子里有几个。

建议内部维护：

```csharp
Dictionary<PrefabType, HashSet<int>> objectsInBoxByType;
Dictionary<int, PrefabType> objectTypeByInstanceId;
```

这里用 `HashSet<int>` 而不是直接用 `int count`，是为了避免重复计数。

---

## 3. 为什么不能只用一个简单的 count

在 Unity 里，一个物体可能有这些情况：

- 一个 prefab 根节点下面带了多个 `Collider`
- 子物体碰撞器也会触发 `OnTriggerEnter`
- 一个物体销毁时不一定会按你预期顺序触发离开事件

如果只写：

```csharp
count++;
count--;
```

很容易出现数量错乱。

更稳的做法是：

1. 进入 `TriggerBox` 时，先找到这个碰撞器对应的“真实 prefab 根对象”。
2. 取这个根对象的 `InstanceID`。
3. 用 `HashSet<int>` 记录它是否已经在盒子里。
4. 只有第一次进入时才算新增。
5. 离开时从 `HashSet<int>` 移除，移除成功才算减少。

这样就不会因为多个碰撞器重复进入而重复计数。

---

## 4. TriggerBox 的判定流程

### 4.1 `OnTriggerEnter`

进入盒子时的流程建议如下：

1. 从 `other` 往父节点查找 `PrefabIdentity`。
2. 如果没找到，直接忽略。
3. 取根对象的 `instanceId`。
4. 判断这个 `instanceId` 是否已经记录过。
5. 如果没有记录：
   - 加入 `objectsInBoxByType[prefabType]`
   - 记录到 `objectTypeByInstanceId`
6. 重新计算当前盒子判定结果。

伪代码：

```csharp
private void OnTriggerEnter(Collider other)
{
    PrefabIdentity identity = other.GetComponentInParent<PrefabIdentity>();
    if (identity == null) return;

    int id = identity.gameObject.GetInstanceID();
    PrefabType type = identity.prefabType;

    if (!objectTypeByInstanceId.ContainsKey(id))
    {
        objectTypeByInstanceId[id] = type;
        objectsInBoxByType[type].Add(id);
        ReevaluateRule();
    }
}
```

---

### 4.2 `OnTriggerExit`

离开盒子时：

1. 同样找到 `PrefabIdentity`
2. 取 `instanceId`
3. 如果这个对象确实在当前盒子的记录中，就移除
4. 移除后重新判定

伪代码：

```csharp
private void OnTriggerExit(Collider other)
{
    PrefabIdentity identity = other.GetComponentInParent<PrefabIdentity>();
    if (identity == null) return;

    int id = identity.gameObject.GetInstanceID();
    if (!objectTypeByInstanceId.TryGetValue(id, out PrefabType type)) return;

    objectTypeByInstanceId.Remove(id);
    objectsInBoxByType[type].Remove(id);
    ReevaluateRule();
}
```

---

## 5. “有没有几种 prefab” 的判定方式

这个需求本质上有两种常见版本。

### 5.1 版本 A：只判断种类是否都出现过

例如：

- 盒子里要同时有 `Pancake`
- 还要有 `Fruit`
- 还要有 `Topping`

那判断条件就是：

```csharp
bool hasPancake = objectsInBoxByType[PrefabType.Pancake].Count > 0;
bool hasFruit = objectsInBoxByType[PrefabType.Fruit].Count > 0;
bool hasTopping = objectsInBoxByType[PrefabType.Topping].Count > 0;

bool success = hasPancake && hasFruit && hasTopping;
```

适用于“只要这几种都在里面就算成功”。

---

### 5.2 版本 B：判断每种的最小数量

例如：

- `Pancake >= 1`
- `Fruit >= 2`
- `Topping >= 1`

那就不能只看有没有，还要看数量：

```csharp
bool success =
    objectsInBoxByType[PrefabType.Pancake].Count >= 1 &&
    objectsInBoxByType[PrefabType.Fruit].Count >= 2 &&
    objectsInBoxByType[PrefabType.Topping].Count >= 1;
```

这个版本更适合做“配方”或“订单”系统。

---

## 6. 推荐把规则做成可配置数据

如果后面不同流程阶段有不同要求，不要把规则硬编码在 `if` 里，建议做成一个配置结构。

例如：

```csharp
[System.Serializable]
public class RequiredPrefabRule
{
    public PrefabType prefabType;
    public int minCount = 1;
}
```

然后在 `TriggerBoxJudge` 里配置：

```csharp
public List<RequiredPrefabRule> requiredRules;
```

判定时统一遍历：

```csharp
private bool CheckAllRules()
{
    foreach (var rule in requiredRules)
    {
        int count = objectsInBoxByType[rule.prefabType].Count;
        if (count < rule.minCount)
        {
            return false;
        }
    }
    return true;
}
```

好处：

- Inspector 可直接配
- 不同 `TriggerBox` 可以配不同规则
- 以后新增类型不用改一堆 if-else

---

## 7. 场上 prefab 最大生成数量控制逻辑

这部分建议直接接在你们现有的 `HandSpawnController.SpawnAtCurrentPoint()` 前面。

当前它的生成流程大致是：

1. 判断是不是放置模式
2. 判断冷却
3. `Instantiate(prefabToSpawn, ...)`

现在应该改成：

1. 判断是不是放置模式
2. 判断冷却
3. 读取 `prefabToSpawn` 对应的 `PrefabIdentity`
4. 问 `SpawnLimitManager.CanSpawn(type)`
5. 如果不能生成，直接 return
6. 如果可以生成，再 `Instantiate`
7. 生成后调用 `RegisterSpawn(type, instance)`

伪代码：

```csharp
public void SpawnAtCurrentPoint()
{
    if (!IsPlacementModuleActive()) return;
    if (Time.time - _lastSpawnTime < spawnCooldown) return;
    if (prefabToSpawn == null || movingPoint == null) return;

    PrefabIdentity identity = prefabToSpawn.GetComponent<PrefabIdentity>();
    if (identity == null) return;

    if (!SpawnLimitManager.Instance.CanSpawn(identity.prefabType))
    {
        return;
    }

    _lastSpawnTime = Time.time;
    GameObject instance = Instantiate(prefabToSpawn, movingPoint.position, Quaternion.identity);
    SpawnLimitManager.Instance.RegisterSpawn(identity.prefabType, instance);
}
```

---

## 8. 什么时候减少场上数量

`aliveCount` 不是只有 `Destroy()` 时才减少，取决于你们游戏定义“场上”是什么意思。

常见有三种策略：

### 8.1 物体被销毁时减少

适合：

- 物体明确会被删除
- 生成上限只限制当前存活对象数

做法：

- 给生成物体挂一个 `SpawnedObjectLife` 脚本
- 在 `OnDestroy()` 里通知 `SpawnLimitManager.UnregisterSpawn(...)`

---

### 8.2 物体被提交到判定区后减少

适合：

- 玩家把食材放进 `TriggerBox` 就视为“已提交”
- 提交后允许继续生成新的同类物体

做法：

- `TriggerBoxJudge` 判定成功后
- 对参与判定的实例做“锁定/消耗/回收”
- 同时通知 `SpawnLimitManager` 把这些对象从场上计数里减掉

---

### 8.3 物体离开有效区域时减少

适合：

- 掉出桌面
- 掉进垃圾桶
- 超出游戏边界

做法：

- 在边界检测脚本中统一调用 `UnregisterSpawn`

---

## 9. 推荐的数据流

完整数据流可以这样设计：

1. 玩家触发手势。
2. `HandSpawnController` 想生成某个 prefab。
3. `SpawnLimitManager` 先检查该类型是否达到上限。
4. 如果没达到上限，则允许生成并登记数量。
5. 玩家把物体移动进 `TriggerBox`。
6. `TriggerBoxJudge` 通过 `OnTriggerEnter` 把该物体记入盒内统计。
7. 每次进入/离开都重新执行规则检查。
8. 满足规则后，触发：
   - UI 提示成功
   - 流程切换 `ProcessManager.SwitchToNextState()`
   - 消耗或锁定盒内物体

---

## 10. 和你们当前流程管理的结合方式

你们已有 `ProcessManager.State`，所以判定逻辑最好和状态绑定。

例如：

- `State == 3`：只允许判定 `Pancake`
- `State == 4`：只允许判定 `Fruit`
- `State == 5`：只允许判定 `Topping`

那么 `TriggerBoxJudge` 可以增加一个字段：

```csharp
public int validState;
```

重新判定前先检查：

```csharp
if (ProcessManager.Instance == null) return;
if (ProcessManager.Instance.State != validState) return;
```

或者更进一步：

- 一个 `TriggerBoxJudge` 支持多套规则
- 根据 `ProcessManager.State` 自动切换当前规则

这样整个判定系统就和做松饼的步骤同步了。

---

## 11. 最容易出错的几个点

### 11.1 一个 prefab 多个 Collider，重复进入

解决办法：

- 永远统计根对象 `InstanceID`
- 不直接统计 `Collider`

### 11.2 物体销毁了，但 TriggerBox 没收到 Exit

解决办法：

- 给生成物体加生命周期脚本
- 在 `OnDestroy()` 时主动通知管理器清理

### 11.3 预览物体被误判成真实物体

你们当前 `HandSpawnController` 里有 `previewPrefab` / `_previewInstance`。

所以需要保证：

- 预览物体不要挂 `PrefabIdentity`
- 或者挂了也要设为 `isJudgeable = false`
- 并且预览物体 Collider 默认关闭

实际上你们当前代码里已经把 preview 的 `Collider` 禁用了，这一点是对的。

### 11.4 同一类型 prefab 有多个来源

例如 `Fruit_A`、`Fruit_B`、`Fruit_C` 都算 `Fruit`。

解决办法：

- 让它们的 `PrefabIdentity.prefabType` 都填成 `Fruit`
- 判定层只关心逻辑类型，不关心美术资源名

---

## 12. 最推荐的最终实现结构

如果要落地成代码，我建议最终拆成下面 4 个脚本：

1. `PrefabIdentity`
   - 挂在每个可生成 prefab 上
   - 定义它属于哪种逻辑类型

2. `SpawnLimitManager`
   - 全局单例
   - 管理每种 prefab 的最大生成数量和当前数量

3. `SpawnedObjectLife`
   - 挂在生成实例上
   - 负责对象销毁、回收、提交时通知 `SpawnLimitManager`

4. `TriggerBoxJudge`
   - 挂在判定盒上
   - 维护盒内对象集合
   - 检查是否满足当前 prefab 组合规则
   - 成功后通知 `ProcessManager`

---

## 13. 一套简单可用的判断标准

如果你现在只是想先把功能做出来，先用下面这套最简单、最稳的标准：

1. 每个 prefab 挂 `PrefabIdentity`
2. 生成前先查 `SpawnLimitManager.CanSpawn`
3. 生成后登记数量
4. `TriggerBoxJudge` 用 `OnTriggerEnter/Exit + HashSet<InstanceID>` 统计盒内对象
5. 每次变化后检查：
   - 指定类型是否都存在
   - 每种数量是否达到最低要求
6. 判定成功后：
   - 切换流程
   - 需要消耗的对象就销毁或回收
   - 同时更新场上数量

这套方案已经足够覆盖你说的“判断一个 triggerbox 中有没有几种 prefab，同时控制 prefab 在场上生成的最大数量”。

---

## 14. 和当前项目最直接的接入点

你们现在最直接可以改的地方是：

- `Assets/Scripts/GestureManager/HandSpawnController.cs`
  - 在 `Instantiate` 前增加“是否达到生成上限”的判断
- 新增一个判定脚本，例如：
  - `Assets/Scripts/LogicManagers/TriggerBoxJudge.cs`
- 新增一个生成数量管理脚本，例如：
  - `Assets/Scripts/LogicManagers/SpawnLimitManager.cs`

这样不会破坏你们现在的手势生成逻辑，只是在它外面再包一层“裁判规则”。

---

## 15. 结论

这类需求最核心的思路是：

- “场上数量控制”和“盒内组合判定”分开做
- “按 prefab 类型统计”而不是按名字统计
- “按对象实例去重”而不是按触发次数计数

只要按这个结构实现，逻辑会比较稳定，也方便后续继续加：

- 不同关卡配方
- 不同步骤判定
- 成功/失败反馈
- 自动进入下一流程

