# MainMenu 中接入 Ultraleap 果酱放置功能方案

## 目标

把现在 `F` 键在表面连续生成果酱/奶油的测试功能，迁移成正式流程中的 `state 4` 果酱阶段功能，并能在 `MainMenu` 场景中完整运行。

最终效果：

1. `state 4` 才允许选择和放置果酱。
2. 果酱种类通过手势识别选择。
3. 果酱放置位置由 Ultraleap 手部的 `x/z` 位置控制，并保留键盘输入作为调试兜底。
4. 按住 `F` 时按固定间隔在当前表面位置生成果酱。
5. 果酱计分只检查菜单要求的果酱种类是否匹配，不计算同心度。
6. `MainMenu` 场景需要补齐当前缺失的流程管理器、手势管理器、生成管理器、UI 和判定对象引用。

## 当前代码现状

### 1. Ultraleap 接入方式

项目里已经有两处 Ultraleap 接入可以参考：

- `Assets/Scripts/GestureManager/GestureManager.cs`
- `Assets/Scripts/GestureManager/HandSpawnController.cs`

共同模式是：

```csharp
public LeapProvider leapProvider;

private void OnEnable()
{
    leapProvider.OnUpdateFrame += OnUpdateFrame;
}

private void OnDisable()
{
    leapProvider.OnUpdateFrame -= OnUpdateFrame;
}

private void OnUpdateFrame(Frame frame)
{
    Hand hand = frame.GetHand(Chirality.Right);
}
```

`HandSpawnController` 目前只读取 `x`：

```csharp
rightHand.PalmPosition.x
rightHand.Index.TipPosition.x
```

这次果酱表面放置需要读取 `x/z` 两个轴：

- `x` 对应左右移动，等价于键盘 `LeftArrow/RightArrow`。
- `z` 对应前后移动，等价于键盘 `UpArrow/DownArrow`。

### 2. 当前 `F` 生成逻辑

现在 `F` 键表面生成在：

- `Assets/Scripts/Cream/CreamSurfacePlacementTester.cs`

核心字段：

```csharp
[SerializeField] private KeyCode spawnKey = KeyCode.F;
[SerializeField] private float continuousSpawnInterval = 0.25f;
```

核心逻辑：

```csharp
if (Input.GetKey(spawnKey) && Time.time >= nextSpawnTime)
{
    SpawnCream();
    nextSpawnTime = Time.time + Mathf.Max(0.01f, continuousSpawnInterval);
}
```

它已经具备：

- 键盘移动光标。
- 向下射线找表面。
- 在射线命中点生成 prefab。
- 连续按住 `F` 按间隔生成。

但它还缺：

- 没有接 `ProcessManager.State == 4`。
- 没有接 Ultraleap `x/z`。
- 没有接手势选择出来的果酱 prefab。
- 生成出来的果酱 prefab 目前没有完整接入 `PrefabIdentity` / `TriggerBoxJudge`。

### 3. 状态机现状

流程状态在：

- `Assets/Scripts/LogicManagers/ProcessManager.cs`

当前状态定义：

```csharp
1: 对话/等待开始
2: 题目准备
3: 放置松饼
4: 放果酱
5: 放 topping
6: 完成/结算对话
7: 结算分数
```

现在 `case 4` 只是开倒计时和 UI：

```csharp
case 4:
    countdownTimer.StartCountdown(15f);
    StartCoroutine(WaitAndSwitch(15f, 4));
    uiManager.TriggerEndPlacePancakeUI();
    uiManager.TriggerPlaceJamUI();
    break;
```

这里需要补上：

- 进入 `state 4` 时先进入果酱手势选择模式。
- 手势识别成功后切换到果酱放置模式。
- 退出 `state 4` 时关闭果酱放置输入。

### 4. 手势 label 到 prefab 的映射在哪里设置

目前映射不是写死在代码里，而是在场景里的 `GestureSpawnController` 对象上配置。

对应脚本：

- `Assets/Scripts/GestureManager/GestureSpawnSelector.cs`

核心数据结构：

```csharp
public List<GesturePrefabMapping> mappings;
```

核心方法：

```csharp
RecognizeAndApply()
ApplyLastRecognizedGesture()
ApplyRecognizedLabel(string label)
```

当前场景配置：

- `Assets/Scenes/MainScene.unity`
  - `gestureLabel: 1` -> `Assets/Prefabs/Strawberry_01.prefab`
  - `gestureLabel: 0` -> `Assets/Prefabs/Cylinder.prefab`
  - `gestureLabel: C` -> `Assets/Prefabs/None.prefab`

- `Assets/Scenes/Pancake_wbh.unity`
  - `gestureLabel: 1` -> `Assets/Prefabs/Strawberry_01.prefab`

手势模板数据在：

- `Assets/Resources/gesture_templates.json`

当前模板里能看到：

- `"name": "1"`
- `"name": "2"`

训练新模板时默认 label 在：

- `Assets/Scripts/GestureManager/GestureManager.cs`

```csharp
public string recordLabel = "6";
```

代码中也有手动调用 label 的地方：

- `ProcessManager.cs`
  - `"C"`：空物体/禁用
  - `"0"`：松饼

- `InputManager.cs`
  - `"C"`：空物体/禁用
  - `"1"`：草莓

## 推荐实现结构

不要直接把果酱逻辑塞进 `CreamSurfacePlacementTester` 或 `HandSpawnController`。现有两个脚本职责不同：

- `CreamSurfacePlacementTester` 是测试脚本，适合参考，但不适合作正式流程入口。
- `HandSpawnController` 是固定点生成，不适合果酱这种表面射线放置。

建议新增一个正式脚本：

- `Assets/Scripts/Cream/JamSurfacePlacementController.cs`

职责：

1. 接 Ultraleap，读取右手 `x/z`。
2. 保留键盘方向键调试输入。
3. 用射线把光标贴到松饼表面。
4. 接收当前要生成的果酱 prefab。
5. 只在 `ProcessManager.State == 4` 且处于放置模式时响应 `F`。
6. 生成后给 `SpawnLimitManager` 登记。

核心字段建议：

```csharp
[Header("Leap Input")]
public LeapProvider leapProvider;
public Chirality handType = Chirality.Right;
public bool useIndexTip = true;

[Header("Input Mapping")]
public float inputMinX = -0.2f;
public float inputMaxX = 0.2f;
public float inputMinZ = -0.2f;
public float inputMaxZ = 0.2f;
public Vector2 sceneMinXZ;
public Vector2 sceneMaxXZ;
public bool autoExpandInputRange = true;

[Header("Surface Placement")]
[SerializeField] private Transform placementCursor;
[SerializeField] private Transform spawnParent;
[SerializeField] private LayerMask surfaceMask = ~0;
[SerializeField] private float rayStartHeight = 0.5f;
[SerializeField] private float rayDistance = 2f;
[SerializeField] private float surfaceOffset = 0.01f;

[Header("Spawn")]
[SerializeField] private GameObject prefabToSpawn;
[SerializeField] private KeyCode spawnKey = KeyCode.F;
[SerializeField] private float continuousSpawnInterval = 0.25f;
```

对外入口建议：

```csharp
public void SetPrefabToSpawn(GameObject prefab)
public void ClearSpawnedJam()
public void CalibrateInputRangeFromCurrentHand()
```

状态判断建议：

```csharp
private bool IsJamPlacementActive()
{
    return ProcessManager.Instance != null
        && ProcessManager.Instance.State == 4
        && ProcessManager.Instance.IsPlacementMode();
}
```

## state 4 的流程设计

`state 4` 需要拆成两个阶段，但仍然保持外部状态编号为 4：

### 阶段 A：选择果酱种类

进入 `state 4` 时：

```csharp
placeMode = 2;
gestureSpawnSelector.ApplyRecognizedLabel("C");
```

含义：

- `placeMode = 2` 让 `GestureManager` / `GestureTemplateRecognizer` 可以记录和识别手势。
- 当前生成 prefab 先清空，避免玩家还没选择果酱就误生成。

玩家完成手势后调用：

```csharp
gestureSpawnSelector.RecognizeAndApply();
```

或者如果别处已经执行识别，则调用：

```csharp
gestureSpawnSelector.ApplyLastRecognizedGesture();
```

### 阶段 B：放置果酱

果酱 prefab 选择成功后：

```csharp
ProcessManager.Instance.SetPrefabToSpawnDone();
```

现在这个方法有问题：它不区分 state，会直接调用：

```csharp
uiManager.EndChooseToppingHint();
```

这对 `state 4` 果酱不合适。需要改成 state-aware：

```csharp
public void SetPrefabToSpawnDone()
{
    if (state == 4)
    {
        placeMode = 1;
        uiManager.EndChooseJamHint(); // 如果新增了选择果酱提示
        return;
    }

    if (state == 5)
    {
        placeMode = 1;
        uiManager.EndChooseToppingHint();
        return;
    }

    if (!IsProhibitedMode())
    {
        placeMode = 1;
    }
}
```

如果暂时不新增 `EndChooseJamHint()`，至少要保证 `state 4` 不调用 `EndChooseToppingHint()`。

## 手势选择如何同时支持 topping 和 jam

当前 `GestureSpawnSelector` 只知道把 label 映射成 prefab，并直接交给 `HandSpawnController`：

```csharp
handSpawnController.SetPrefabToSpawn(mappedPrefab);
```

果酱不能继续只走这条路，因为果酱要表面射线生成。推荐把 `GestureSpawnSelector` 改成按当前 state 分流：

```csharp
if (ProcessManager.Instance != null && ProcessManager.Instance.State == 4)
{
    jamSurfacePlacementController.SetPrefabToSpawn(mappedPrefab);
}
else
{
    handSpawnController.SetPrefabToSpawn(mappedPrefab);
}
```

需要给 `GestureSpawnSelector` 增加引用：

```csharp
[SerializeField] private JamSurfacePlacementController jamSurfacePlacementController;
```

这样：

- `state 4`：label -> 果酱 prefab -> `JamSurfacePlacementController`
- `state 5`：label -> topping prefab -> `HandSpawnController`

如果想结构更干净，可以后续把选择目标抽象成接口，但 Unity Inspector 对接口序列化不方便，当前项目更适合先用显式引用。

## 果酱 prefab 和 PrefabType 需要补齐

当前 `PrefabType` 是：

```csharp
public enum PrefabType
{
    Pancake,
    Fruit,
    Topping,
    Cream,
    Syrup
}
```

当前真正带 `PrefabIdentity` 的 prefab 只有：

- `Assets/Prefabs/Cylinder.prefab` -> `Pancake`
- `Assets/Prefabs/Panpake_c.prefab` -> `Pancake`
- `Assets/Prefabs/Strawberry_01.prefab` -> `Fruit`

`Assets/Prefabs/cream/CreamSphereCluster.prefab` 现在没有 `PrefabIdentity`。如果它要作为果酱参与菜单和计分，必须补上。

因为需求是“果酱种类和菜单对应即可”，只用一个 `PrefabType.Syrup` 或 `PrefabType.Cream` 不够区分果酱种类。推荐把果酱类型显式加入 enum，例如：

```csharp
public enum PrefabType
{
    Pancake,
    Fruit,
    Topping,
    StrawberryJam,
    BlueberryJam,
    ChocolateJam,
    Cream,
    Syrup
}
```

然后每个果酱 prefab 都挂：

```text
PrefabIdentity
prefabType = StrawberryJam / BlueberryJam / ChocolateJam
isJudgeable = true
countsTowardSpawnLimit = true
```

这样 `RuntimeJudgeRecipe`、`RandomRecipeGenerator`、`ReadyRecipeUI`、`TriggerBoxJudge` 都可以继续用现有的 `PrefabType` 接口，不需要新建一套 recipe key。

## 菜单生成需要加入果酱

当前菜单生成配置在：

- `Assets/Scripts/GadingManager/JudgeRecipeGenerationConfig.asset`

当前规则只有：

```text
Pancake
Fruit
```

需要新增果酱规则，例如：

```text
StrawberryJam required/min/max
BlueberryJam required/min/max
ChocolateJam required/min/max
```

如果每轮只要求一种果酱，建议：

- 果酱组至少选 1 种。
- 每种果酱 requiredCount 为 1。
- 生成器需要支持“同一组中随机选一个”的规则。

当前 `RandomRecipeGenerator` 是按独立 `RecipeGenerationRule` 计数，不支持互斥组。这里有两个实现选择：

1. 简单版：配置所有果酱规则权重，让生成器随机抽到其中一种，允许理论上出现多种果酱。
2. 正式版：给 `RecipeGenerationRule` 增加 `groupId` 和 `maxTypesFromGroup`，例如 `groupId = "jam"`，每轮只从 jam 组抽 1 种。

为了保证菜单不会要求多个果酱，推荐做正式版。

## 果酱计分不算同心度

当前判定在：

- `Assets/Scripts/GadingManager/TriggerBoxJudge.cs`

现在总分是：

```csharp
lastRecipeScore = CalculateRecipeScore(...);
lastConcentricityScore = CalculateConcentricityScore(...);
lastTotalScore = lastRecipeScore + lastConcentricityScore;
```

这会把所有可判定物体都纳入同心度，包括果酱。需要改成：

1. 果酱仍然参与配方匹配。
2. 果酱不参与同心度。
3. 果酱只检查种类和数量是否符合菜单。

推荐加一个判定辅助方法：

```csharp
private static bool CountsTowardConcentricity(PrefabType type)
{
    switch (type)
    {
        case PrefabType.StrawberryJam:
        case PrefabType.BlueberryJam:
        case PrefabType.ChocolateJam:
            return false;
        default:
            return true;
    }
}
```

然后在 `CalculateConcentricityScore()` 里跳过果酱类型。

如果希望更可配，后续可以把这个规则放到 `PrefabIdentity`：

```csharp
[SerializeField] private bool countsTowardConcentricity = true;
```

但当前项目所有判定都以 `PrefabType` 为 key，先按 `PrefabType` 判断更容易接入现有接口。

## MainMenu 场景需要补齐的对象

现在 `Assets/Scenes/MainMenu.unity` 基本只有：

- `Main Camera`
- `Directional Light`

没有完整 gameplay 所需对象。要把这个功能迁到 `MainMenu`，至少要补齐：

1. `ProcessManager`
2. `InputManager`
3. `UIManager`
4. `RecipeRoundController`
5. `TriggerBoxJudge` + `BoxCollider`
6. `SpawnLimitManager`
7. `GestureManager`
8. `GestureTemplateRecognizer`
9. `GestureSpawnSelector`
10. `JamSurfacePlacementController`
11. Ultraleap 的 `LeapProvider`
12. 果酱生成父节点 `JamSpawnedRoot`
13. 果酱放置光标 `JamPlacementCursor`
14. 可被射线命中的松饼/盘子表面 Layer

可以直接参考 `MainScene.unity` 的这些对象配置：

- `HandSpawnController`
- `GestureSpawnController`
- `TriggerBox`
- `ProcessManager`
- UI Canvas prefab

但要注意：`MainMenu` 如果仍然作为真正的主菜单使用，就不应该直接启动完整 gameplay。更合理的做法是：

- 如果 `MainMenu` 只是测试场景：可以直接放完整流程对象。
- 如果 `MainMenu` 是正式菜单：应该新建一个 Jam 测试场景，或者通过按钮进入 gameplay 场景。

## 输入触发建议

为了和现在项目保持一致，保留这些调试键：

- `R`：识别当前手势，调用 `GestureTemplateRecognizer.Recognize()`。
- `X`：保存模板，调用 `GestureTemplateRecognizer.SaveTemplate()`。
- `C`：清空笔迹，调用 `GestureTemplateRecognizer.ClearDrawing()`。
- `F`：在果酱放置阶段连续生成果酱。
- 方向键：没有 Ultraleap 时移动果酱光标。

正式流程建议：

1. `state 4` 开始后显示“选择果酱”提示。
2. 玩家用手势绘制果酱 label。
3. 识别完成后调用 `GestureSpawnSelector.RecognizeAndApply()`。
4. 成功选择后 `ProcessManager.SetPrefabToSpawnDone()` 把 `placeMode` 切到 1。
5. 玩家用 Ultraleap 控制 `x/z` 放置位置。
6. 按住 `F` 生成果酱。
7. 15 秒结束或流程提前结束后进入 `state 5`。

## 需要改动的文件清单

### 新增文件

- `Assets/Scripts/Cream/JamSurfacePlacementController.cs`
  - 正式果酱表面放置控制器。

### 修改文件

- `Assets/Scripts/GestureManager/GestureSpawnSelector.cs`
  - 增加 `JamSurfacePlacementController` 引用。
  - 按 `ProcessManager.State` 把 prefab 分发给果酱放置或 topping 放置。

- `Assets/Scripts/LogicManagers/ProcessManager.cs`
  - `case 4` 进入手势选择模式。
  - `SetPrefabToSpawnDone()` 改成 state-aware。
  - 退出 `state 4` 时清理果酱 UI 和输入。

- `Assets/Scripts/PancakeManager/PrefabIdentity.cs`
  - 增加果酱具体类型，例如 `StrawberryJam`、`BlueberryJam`。

- `Assets/Scripts/GadingManager/TriggerBoxJudge.cs`
  - 果酱参与 recipe 匹配。
  - 果酱不参与同心度计算。

- `Assets/Scripts/GadingManager/JudgeRecipeGenerationConfig.asset`
  - 加入果酱规则。

- `Assets/Scripts/LogicManagers/RecipeUI/ReadyRecipeUI.cs`
  - 给果酱类型配置图标。

- `Assets/Scenes/MainMenu.unity`
  - 补齐运行所需对象和引用。

### 需要改的 prefab

- 所有果酱 prefab
  - 添加 `PrefabIdentity`。
  - 设置正确 `PrefabType`。
  - 保证有 Collider，能被 `TriggerBoxJudge` 扫描到。

- `CreamSphereCluster.prefab`
  - 如果继续作为果酱视觉 prefab 使用，需要补 `PrefabIdentity`。

## 容易遗漏的接口点

1. `GestureManager` 只在 `ProcessManager.Instance.IsGestureMode()` 为 true 时记录手势，所以 `state 4` 开始必须先设置 `placeMode = 2`。
2. `JamSurfacePlacementController` 只应该在 `state 4 + placeMode 1` 响应 `F`，否则会和 topping/pancake 阶段互相干扰。
3. `GestureSpawnSelector.ApplyRecognizedLabel()` 当前一定调用 `ProcessManager.SetPrefabToSpawnDone()`，这个方法必须按 state 区分 jam 和 topping。
4. 果酱 prefab 如果没有 `PrefabIdentity`，`TriggerBoxJudge` 会完全忽略它。
5. 果酱 prefab 如果没有 Collider，`Physics.OverlapBox` 扫不到它。
6. `ReadyRecipeUI` 如果没配果酱图标，菜单会显示 fallback icon 或缺图。
7. `SpawnLimitManager` 如果要限制每种果酱只放一个，需要给果酱 `PrefabType` 配 maxCount。
8. 新一轮开始时需要清理上一轮果酱对象，或者确保 `SpawnedObjectLife` / `PrefabToSpawn` 的清理逻辑覆盖果酱。
9. `MainMenu` 如果没有 `LeapProvider`，所有 Ultraleap 相关脚本都只会走键盘兜底。
10. 表面射线 `surfaceMask` 必须只包含可放置表面，避免射到 UI、手模型或已生成的果酱。

## 建议实施顺序

1. 先在 `MainScene` 或 `Pancake_wbh` 中验证 `JamSurfacePlacementController`，不要一开始就在空的 `MainMenu` 场景里调。
2. 给果酱 prefab 补 `PrefabIdentity` 和 Collider。
3. 修改 `GestureSpawnSelector`，让 `state 4` 的 label 选择进入果酱控制器。
4. 修改 `ProcessManager case 4`，把果酱阶段拆成“手势选择”和“表面放置”两个内部阶段。
5. 修改 `TriggerBoxJudge`，让果酱跳过同心度。
6. 更新 recipe 配置和 Ready UI 图标。
7. 最后把完整对象迁移到 `MainMenu.unity`，连好 Inspector 引用。

## 最终数据流

```text
ProcessManager state 4
        |
        v
placeMode = 2，启用手势输入
        |
        v
GestureManager 记录 Ultraleap 手势
        |
        v
GestureTemplateRecognizer.RecognizeLabel()
        |
        v
GestureSpawnSelector 根据 label 找果酱 prefab
        |
        v
JamSurfacePlacementController.SetPrefabToSpawn(prefab)
        |
        v
ProcessManager.SetPrefabToSpawnDone()
        |
        v
placeMode = 1，启用果酱放置
        |
        v
Ultraleap x/z 或方向键移动 cursor
        |
        v
按住 F，按间隔在表面生成果酱
        |
        v
TriggerBoxJudge 在 state 6 判定种类是否匹配菜单
```

