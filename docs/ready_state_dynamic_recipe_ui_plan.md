# ReadyState 动态菜单 UI 实现思路

## 目标

ReadyState 阶段不再显示一张固定的 `Recipe Image`，而是根据当前随机生成的 recipe 动态显示菜单内容。

显示形式：

```text
物体   数量
```

例如：

```text
[Pancake Icon]   2
[Jam Icon]    1
[Strawberry Icon] *  3
```

这里的关键要求是：Ready UI 显示的菜单和 Judge 判分使用的菜单必须是同一份数据，不能 UI 自己生成一份，Judge 再生成另一份。

## 现有生成逻辑

当前已经有两个核心文件：

```text
Assets/Scripts/GadingManager/JudgeRecipeGenerationConfig.cs
Assets/Scripts/GadingManager/RandomRecipeGenerator.cs
```

它们的职责应该保持不变。

### JudgeRecipeGenerationConfig

`JudgeRecipeGenerationConfig` 是一个 `ScriptableObject`，负责配置随机菜单生成规则。

它里面的每条规则是：

```csharp
public class RecipeGenerationRule
{
    public PrefabType prefabType;
    public bool required;
    public int minCount;
    public int maxCount;
    public int weight;
}
```

也就是说，`JudgeRecipeGenerationConfig` 决定：

```text
哪些 PrefabType 可以出现在菜单里
哪些类型是必选的
每种类型最少几个
每种类型最多几个
每种类型被随机选中的权重
整个菜单总数量范围
是否拒绝额外类型 rejectUnexpectedTypes
```

这个文件不应该负责 UI，也不应该持有 icon。

### RandomRecipeGenerator

`RandomRecipeGenerator` 是生成器，负责读取 `JudgeRecipeGenerationConfig`，然后生成一份运行时菜单：

```csharp
RuntimeJudgeRecipe
```

生成结果里真正有用的数据是：

```csharp
RuntimeJudgeRecipe.Requirements
```

其中每一项是：

```csharp
public class JudgeRequirementEntry
{
    public PrefabType prefabType;
    public int requiredCount;
}
```

这就是 Ready UI 需要显示的内容，也是 Judge 需要判定的内容。

## 当前正确的数据流

当前流程应该保持为：

```text
JudgeRecipeGenerationConfig
        ↓
RandomRecipeGenerator.TryGenerate(...)
        ↓
RuntimeJudgeRecipe
        ↓
RecipeRoundController.currentRecipe
        ↓
同时给 Ready UI 和 TriggerBoxJudge 使用
```

在 `RecipeRoundController.GenerateAndApplyRecipe()` 里，目前已经做了关键事情：

```csharp
currentRecipe = generatedRecipe;
targetJudge.SetRuntimeRecipe(generatedRecipe);
```

这说明生成出来的 `RuntimeJudgeRecipe` 已经被交给 Judge 使用了。

Ready UI 不应该再重新调用 `RandomRecipeGenerator`。它应该读取同一份 `RuntimeJudgeRecipe`：

```csharp
recipeRoundController.CurrentRecipe
```

这样可以保证：

```text
玩家看到的菜单 == Judge 判分使用的菜单
```

## ProcessManager 中的接入点

当前 `ProcessManager` 在 `state 2` 里先生成 recipe，再显示 Ready UI：

```csharp
recipeRoundController.GenerateApplyAndJudge();
uiManager.TriggerReadyStateUI();
```

这个顺序是对的，因为 UI 显示前 recipe 已经生成。

建议改成：

```csharp
recipeRoundController.GenerateApplyAndJudge();
uiManager.TriggerReadyStateUI(recipeRoundController.CurrentRecipe);
```

然后把 `UIManager.TriggerReadyStateUI()` 改成：

```csharp
public void TriggerReadyStateUI(RuntimeJudgeRecipe recipe)
```

这样 `ProcessManager` 明确把“本轮生成出来的菜单”传给 UI，UI 不需要知道菜单是谁生成的。

## Ready UI 应该怎样生成

原来 `UIManager` 里有：

```csharp
[SerializeField] private Image recipeImage;
```

这个字段代表一整张静态 recipe 图片，不适合现在的需求。

建议替换成：

```csharp
[SerializeField] private ReadyRecipeUI readyRecipeUI;
```

`ReadyRecipeUI` 专门负责把 `RuntimeJudgeRecipe` 渲染成 UI item。

推荐 API：

```csharp
public class ReadyRecipeUI : MonoBehaviour
{
    public void Render(RuntimeJudgeRecipe recipe);
    public void Clear();
}
```

`UIManager.ReadyStateUI()` 的节奏可以保持不变，只是把显示图片改成显示动态 panel：

```csharp
readyRecipeUI.Clear();
readyRecipeUI.gameObject.SetActive(false);

yield return new WaitForSeconds(0.5f);

readyRecipeUI.Render(recipe);
readyRecipeUI.gameObject.SetActive(true);
```

后面的 `Ready?-Text`、`Cook!-Text` 显示逻辑可以继续沿用。

## 推荐 UI 层级

把 `ReadyState` 下原来的 `Recipe-Image` 换成一个 panel。

推荐结构：

```text
ReadyState
├── RecipePanel
│   ├── RecipeRequirementItem instance
│   │   ├── Icon Image
│   │   ├── Multiply Text: "*"
│   │   └── Count Text: "2"
│   ├── RecipeRequirementItem instance
│   └── ...
├── Ready?-Text
└── Cook!-Text
```

`RecipePanel` 建议挂：

```text
HorizontalLayoutGroup
```

如果以后菜单项更多，改成：

```text
GridLayoutGroup
```

## RecipeRequirementItem Prefab

新增一个 item prefab：

```text
Assets/Prefabs/UI/RecipeRequirementItem.prefab
```

结构：

```text
RecipeRequirementItem
├── Icon      Image
└── Count     TextMeshProUGUI
```

脚本建议：

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeRequirementItemUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;

    public void Set(Sprite icon, int count)
    {
        iconImage.sprite = icon;
        countText.text = count.ToString();
    }
}
```

这个 item 只负责显示，不负责生成 recipe，也不直接访问 Judge。

## PrefabType 到 Icon 的映射

`RandomRecipeGenerator` 只应该生成 `PrefabType + requiredCount`，不应该关心 UI icon。

Icon 是显示层数据，建议放在 `ReadyRecipeUI` 中通过 Inspector 配置：

```csharp
[System.Serializable]
public class RecipeIconEntry
{
    public PrefabType prefabType;
    public Sprite icon;
}
```

`ReadyRecipeUI` 里持有：

```csharp
[SerializeField] private Transform recipePanelRoot;
[SerializeField] private RecipeRequirementItemUI recipeItemPrefab;
[SerializeField] private Sprite fallbackIcon;
[SerializeField] private List<RecipeIconEntry> recipeIcons;
```

Inspector 配置示例：

```text
PrefabType.Pancake    -> pancake icon
PrefabType.Jam        -> jam icon
PrefabType.Strawberry -> strawberry icon
```

这里必须用 `PrefabType` 做 key，因为 `JudgeRecipeGenerationConfig`、`RandomRecipeGenerator`、`RuntimeJudgeRecipe`、`TriggerBoxJudge` 都是基于 `PrefabType` 判断物体类型。

## ReadyRecipeUI 的 Render 逻辑

`Render(RuntimeJudgeRecipe recipe)` 建议逻辑：

```text
1. Clear() 清空上一次生成的 item。
2. 如果 recipe == null，显示空 panel 或 fallback 文本。
3. 遍历 recipe.Requirements。
4. 跳过 null entry。
5. 跳过 requiredCount <= 0 的 entry。
6. 用 entry.prefabType 查找 icon。
7. 如果找不到 icon，使用 fallbackIcon，并 Debug.LogWarning。
8. Instantiate 一个 RecipeRequirementItemUI。
9. 调用 item.Set(icon, entry.requiredCount)。
```

伪代码：

```csharp
public void Render(RuntimeJudgeRecipe recipe)
{
    Clear();

    if (recipe == null)
    {
        return;
    }

    foreach (JudgeRequirementEntry entry in recipe.Requirements)
    {
        if (entry == null || entry.requiredCount <= 0)
        {
            continue;
        }

        Sprite icon = GetIcon(entry.prefabType);
        RecipeRequirementItemUI item = Instantiate(recipeItemPrefab, recipePanelRoot);
        item.Set(icon, entry.requiredCount);
    }
}
```

## 不推荐的做法

不要让 Ready UI 自己调用：

```csharp
RandomRecipeGenerator.TryGenerate(...)
```

原因是这样会生成第二份 recipe，导致：

```text
Ready UI 显示的是 A 菜单
Judge 判分用的是 B 菜单
```

这是最需要避免的问题。

也不建议把 icon 配置写进 `JudgeRecipeGenerationConfig`，因为这个 config 是生成规则，不是 UI 配置。保持它只描述生成逻辑更清晰。

## 推荐实施步骤

1. 保留 `JudgeRecipeGenerationConfig.cs` 和 `RandomRecipeGenerator.cs` 的生成逻辑不变。
2. 新增 `RecipeRequirementItemUI.cs`。
3. 新增 `ReadyRecipeUI.cs`。
4. 新建 `RecipeRequirementItem.prefab`。
5. 在 `Canvas.prefab` 的 `ReadyState` 下用 `RecipePanel` 替换 `Recipe-Image`。
6. 给 `RecipePanel` 添加 `HorizontalLayoutGroup` 或 `GridLayoutGroup`。
7. 在 `ReadyRecipeUI` Inspector 中配置 `PrefabType -> Sprite icon`。
8. 修改 `UIManager.TriggerReadyStateUI(RuntimeJudgeRecipe recipe)`。
9. 修改 `ProcessManager state 2`，把 `recipeRoundController.CurrentRecipe` 传给 UI。
10. 确认 `TriggerBoxJudge.SetRuntimeRecipe(generatedRecipe)` 仍然使用同一份 recipe。

## 最终数据关系

最终应该是：

```text
JudgeRecipeGenerationConfig.asset
配置可生成哪些 PrefabType、数量范围、权重

RandomRecipeGenerator
根据 config 生成 RuntimeJudgeRecipe

RecipeRoundController
保存 currentRecipe，并传给 TriggerBoxJudge

TriggerBoxJudge
使用 currentRecipe.Requirements 判分

ReadyRecipeUI
使用同一个 currentRecipe.Requirements 生成 icon * count UI
```

这样结构最重要的好处是：

```text
生成逻辑只有一份
菜单数据只有一份
UI 和 Judge 永远读取同一个 RuntimeJudgeRecipe
```
