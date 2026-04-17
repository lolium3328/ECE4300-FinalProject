# Gesture 识别结果驱动 Spawn 的实现方案

## 当前状态说明

截至目前，文档中的前三步已经落地：

1. `GestureManager` 已增加只读属性，能稳定暴露最后一次识别结果
2. `HandSpawnController` 已增加 `SetPrefabToSpawn()`，切换时会刷新预览
3. `GestureSpawnSelector.cs` 已创建，用于把识别标签映射成 prefab 并应用到 `HandSpawnController`

当前这份文档保留为“总体设计方案”，具体已实现的数据流和场景接法，见：

- `docs/gesture_spawn_data_flow.md`

## 目标

你现在想做的事情可以拆成两步：

1. 把 `GestureManager` 里识别出来的 `bestMatch` 结果稳定地暴露给外部使用。
2. 新建一个独立脚本，根据这个识别结果决定 `HandSpawnController` 当前要 `Spawn` 哪个物体。

这件事不建议把全部逻辑直接塞进 `GestureManager` 或 `HandSpawnController`，更稳妥的做法是拆成：

- `GestureManager`
  负责手势识别，输出最后一次识别结果。
- `GestureSpawnSelector`（新文件）
  负责把识别结果映射成具体 prefab。
- `HandSpawnController`
  负责真正生成物体，并提供“切换当前 prefab”的入口。

---

## 当前代码现状

项目里现在已经有这些基础：

- `Assets/Scripts/GestureManager/GestureManager.cs`
  - 内部会在 `Recognize()` 里得到 `bestMatch`
  - 然后执行：

```csharp
lastRecognizedLabel = bestMatch;
```

- `GestureManager` 目前已经有：

```csharp
public string GetLastRecognizedLabel()
{
    return lastRecognizedLabel;
}
```

也就是说，严格来说“最后一次识别结果”已经能拿到，但目前还没有一个专门脚本把这个结果和 `HandSpawnController.prefabToSpawn` 连接起来。

另外有一个实际问题：

- `HandSpawnController` 当前的预览物体 `_previewInstance` 是按初始 `prefabToSpawn` 建出来的。
- 如果后面直接在别的脚本里改 `prefabToSpawn`，预览模型大概率不会自动更新。

所以这次实现时，不能只做“读标签 + 改变量”，还应该补一个正式的切换入口。

---

## 我打算怎么改

### 1. 在 `GestureManager` 中正式暴露识别结果

目标不是只靠一行赋值，而是让外部脚本有一个明确、稳定的访问方式。

计划做法：

- 保留现有这行：

```csharp
lastRecognizedLabel = bestMatch;
```

- 再补一个公开只读属性，类似：

```csharp
public string LastRecognizedLabel => lastRecognizedLabel;
```

这样外部既可以继续用现有的 `GetLastRecognizedLabel()`，也可以直接通过属性读取，语义更清晰。

如果你希望后续别的模块在“识别完成的瞬间”立刻收到结果，我还会考虑再加一个事件，例如：

```csharp
public event System.Action<string> OnGestureRecognized;
```

并在识别成功后调用：

```csharp
OnGestureRecognized?.Invoke(bestMatch);
```

当前阶段我更倾向于先做“公开属性 + 明确调用入口”，先把链路跑通，再决定要不要加事件。

---

### 2. 新建一个独立脚本 `GestureSpawnSelector.cs`

建议新文件位置：

- `Assets/Scripts/GestureManager/GestureSpawnSelector.cs`

这个脚本的职责非常单一：

- 从 `GestureManager` 或 `GestureTemplateRecognizer` 读取最后一次识别结果
- 根据识别出来的标签，选择对应的 prefab
- 通知 `HandSpawnController` 切换当前要生成的物体

我不会把识别逻辑复制到这个脚本里，它只负责“识别结果 -> prefab”的映射。

建议结构如下：

```csharp
[System.Serializable]
public class GesturePrefabMapping
{
    public string gestureLabel;
    public GameObject prefab;
}

public class GestureSpawnSelector : MonoBehaviour
{
    public GestureTemplateRecognizer recognizer;
    public HandSpawnController handSpawnController;
    public List<GesturePrefabMapping> mappings;

    public void ApplyLastRecognizedGesture()
    {
        string label = recognizer.GetLastRecognizedLabel();
        // 根据 label 找到 prefab
        // 然后交给 handSpawnController 切换
    }
}
```

这里我建议直接依赖 `GestureTemplateRecognizer`，因为 `GestureManager.cs` 头部自己也写了注释：

- 外部游戏逻辑优先通过 `GestureTemplateRecognizer` 路由，而不是直接调 `GestureManager`

这样后续结构会更干净。

---

### 3. 在 `GestureSpawnSelector` 里做“数字标签 -> prefab”映射

你的 `bestMatch` 现在看起来是字符串标签，例如 `"1"`、`"2"`、`"6"` 这种。

所以判断方式可以有两种：

#### 方案 A：直接按字符串匹配

例如：

```csharp
if (label == "1") { ... }
else if (label == "2") { ... }
```

这个能跑，但扩展性一般。

#### 方案 B：Inspector 可配置映射

更推荐做成：

```csharp
public List<GesturePrefabMapping> mappings;
```

这样你可以在 Unity Inspector 里配置：

- `"1"` -> `Pancake`
- `"2"` -> `Strawberry`
- `"3"` -> `Cylinder`

好处是：

- 后面改手势编号时，不用再改代码
- 你们组里其他人也能直接在 Inspector 上配
- 比写一堆 `if/else` 更稳

如果你明确只会有 2 到 3 个固定编号，也可以先上 `if/else`，但从项目结构看，做成可配置映射更合适。

---

### 4. 给 `HandSpawnController` 增加一个正式切换入口

当前 `HandSpawnController` 有：

```csharp
public GameObject prefabToSpawn;
```

但是如果外部脚本直接赋值：

```csharp
handSpawnController.prefabToSpawn = xxx;
```

会有两个风险：

1. 预览物体不会同步刷新
2. 以后如果你还想加切换时的日志、校验、默认回退，外部全都要重写

所以我会在 `HandSpawnController` 里加一个方法，类似：

```csharp
public void SetPrefabToSpawn(GameObject newPrefab)
{
    prefabToSpawn = newPrefab;
    RefreshPreview();
}
```

然后内部再补一个 `RefreshPreview()`：

- 销毁旧的 `_previewInstance`
- 按新的 `prefabToSpawn` 重新创建预览
- 保持现有透明材质和旋转逻辑

这样 `GestureSpawnSelector` 就只负责调用：

```csharp
handSpawnController.SetPrefabToSpawn(targetPrefab);
```

职责会非常清楚。

---

## 预期调用链

最终我打算把流程整理成这样：

1. 用户完成一次手势书写
2. 触发 `GestureTemplateRecognizer.Recognize()` 或 `RecognizeLabel()`
3. `GestureManager` 内部得到 `bestMatch`
4. `GestureManager` 保存：

```csharp
lastRecognizedLabel = bestMatch;
```

5. `GestureSpawnSelector` 读取这个结果
6. `GestureSpawnSelector` 根据映射找到目标 prefab
7. `GestureSpawnSelector` 调用：

```csharp
handSpawnController.SetPrefabToSpawn(targetPrefab);
```

8. 后续用户执行放置操作时，`HandSpawnController.SpawnAtCurrentPoint()` 生成的就是对应物体

---

## 具体文件改动计划

### 文件 1

- `Assets/Scripts/GestureManager/GestureManager.cs`

计划改动：

- 保留现有 `lastRecognizedLabel = bestMatch;`
- 增加只读公开属性 `LastRecognizedLabel`
- 如有必要，补一个识别完成事件

---

### 文件 2

- `Assets/Scripts/GestureManager/HandSpawnController.cs`

计划改动：

- 增加 `SetPrefabToSpawn(GameObject newPrefab)`
- 增加 `RefreshPreview()`
- 确保切换 prefab 后，预览模型也同步更新

---

### 文件 3

- `Assets/Scripts/GestureManager/GestureSpawnSelector.cs`（新建）

计划改动：

- 定义 `GesturePrefabMapping`
- 持有 `GestureTemplateRecognizer`
- 持有 `HandSpawnController`
- 提供一个方法：
  - 读取最后一次识别标签
  - 按标签匹配 prefab
  - 调用 `SetPrefabToSpawn()`

---

## 为什么这样拆比直接写在一个脚本里更好

如果把所有逻辑都写进 `GestureManager`，会有几个问题：

- `GestureManager` 既管识别，又管生成逻辑，职责混乱
- 后续如果换掉生成系统，识别代码也得改
- 调试时不容易分清是“识别错了”还是“映射错了”

如果拆成 `GestureManager + GestureSpawnSelector + HandSpawnController`：

- 手势识别独立
- prefab 选择独立
- 生成逻辑独立

后续排查问题会很直接：

- 标签不对，看 `GestureManager`
- 标签对但物体不对，看 `GestureSpawnSelector`
- 物体对但没生成出来，看 `HandSpawnController`

---

## 我建议的默认实现顺序

如果下一步要我直接动代码，我会按这个顺序做：

1. 在 `GestureManager` 补公开只读属性
2. 在 `HandSpawnController` 补 `SetPrefabToSpawn()` 和预览刷新
3. 新建 `GestureSpawnSelector.cs`
4. 做 Inspector 映射表
5. 跑一遍识别 -> 切换 prefab -> Spawn 的完整链路

---

## 最后说明

这份方案的核心不是“单纯把 `bestMatch` 暴露出来”，而是把整条链路补完整：

- 识别结果能被外部稳定读取
- 映射逻辑有独立脚本承接
- `HandSpawnController` 能正确切换生成物体并刷新预览

如果你要，我下一步可以直接按照这份文档开始实现代码。
