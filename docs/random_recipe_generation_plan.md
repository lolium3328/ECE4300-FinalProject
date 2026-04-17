# 随机生成 Recipe 并接入 Judge 系统的实现思路

## 1. 目标

当前 `TriggerBoxJudge` 已经具备两件事：

1. 扫描判定区内的物体
2. 按一份 recipe requirement 做数量判定和打分

下一步要解决的问题不是“怎么判”，而是“当前这一轮要判什么”。

也就是说，recipe 不应该继续只是一份固定配置，而应该在每一轮开始时：

1. 按给定约束随机生成一份 recipe
2. 把这份 recipe 交给 judge
3. 同时把这份 recipe 展示给玩家
4. 本轮结算时仍然沿用现有 judge 的扫描和评分逻辑

这里的核心原则是：

- `TriggerBoxJudge` 只负责判定，不负责生成 recipe
- recipe 生成逻辑独立出去，作为上游模块
- 运行时生成的数据不要直接写回资产文件
- 固定 recipe 和随机 recipe 要能共存，方便调试和回退

---

## 2. 推荐的整体结构

我建议把系统拆成三层：

### 2.1 静态约束层

这一层描述“允许生成什么样的 recipe”，它是设计师在 Inspector 里配置的资产。

例如：

- 允许出现哪些 `PrefabType`
- 每种类型最少几个、最多几个
- 总物体数范围
- 某些类型是否必选
- 是否允许出现 recipe 之外的类型
- 某一流程状态下只生成特定类型组合

这一层建议用 `ScriptableObject` 表达，因为它本质是“生成规则模板”，不是本轮实际结果。

建议新增一个类似下面职责的配置：

- `JudgeRecipeGenerationConfig`

它不是最终 recipe，而是“recipe 生成器的输入约束”。

### 2.2 运行时 recipe 层

这一层描述“本轮真正要交给 judge 的 recipe”。

它应该是一份纯运行时数据，而不是资产。

建议单独定义一个运行时数据结构，例如：

```csharp
[System.Serializable]
public class RuntimeJudgeRecipe
{
    public string recipeId;
    public string displayName;
    public List<JudgeRequirementEntry> requirements = new();
    public bool rejectUnexpectedTypes = true;
}
```

这里要注意一点：

- 运行时 recipe 只是本局数据，不建议在运行时 `CreateInstance<ScriptableObject>` 再到处传
- 更不建议生成后写回某个 `.asset`

原因很简单：这会把“本轮结果”和“设计时模板”混在一起，后面会很难维护

### 2.3 生成与调度层

这一层负责在合适的时机生成 recipe，并把它交给 judge / UI / 流程系统。

建议单独放一个控制器，例如：

- `RecipeRoundController`

它的职责：

1. 在一轮开始时请求生成 recipe
2. 保存本轮当前 recipe
3. 把 recipe 传给 `TriggerBoxJudge`
4. 把 recipe 传给 UI
5. 结算后清理或切换到下一轮

---

## 3. 为什么不要让 TriggerBoxJudge 自己随机

如果把“随机 recipe”直接塞进 `TriggerBoxJudge`，短期看似省事，但会把职责搅乱。

`TriggerBoxJudge` 当前天然适合做的是：

- 接收一份目标 requirement
- 扫描场景
- 输出结果

它不适合做的是：

- 决定这一轮出什么题
- 管理随机种子
- 处理难度曲线
- 给 UI 发送 recipe 文案
- 跟流程状态绑定做轮次切换

一旦这些都堆进去，`TriggerBoxJudge` 很快就会变成“既出题又判题又结算”的混合脚本，后面很难扩展。

所以推荐保持这个边界：

- `TriggerBoxJudge` = 判题器
- `RandomRecipeGenerator` = 出题器
- `RecipeRoundController` = 本轮调度器

---

## 4. 建议新增的数据结构

## 4.1 生成规则条目

可以定义一个生成约束条目：

```csharp
[System.Serializable]
public class RecipeGenerationRule
{
    public PrefabType prefabType;
    public bool required;
    public int minCount = 0;
    public int maxCount = 0;
    public int weight = 1;
}
```

含义：

- `prefabType`：这个类型是否可参与生成
- `required`：这一类是否必须出现在 recipe 中
- `minCount / maxCount`：本轮生成时该类型允许的数量范围
- `weight`：随机时的权重

### 4.2 生成配置资产

```csharp
[CreateAssetMenu(...)]
public class JudgeRecipeGenerationConfig : ScriptableObject
{
    public string configName;
    public int minTotalCount = 1;
    public int maxTotalCount = 5;
    public bool rejectUnexpectedTypes = true;
    public List<RecipeGenerationRule> rules = new();
}
```

它的职责是回答：

- 这一轮总共想出多少个材料
- 哪些类型可以参与
- 每种类型的上下限是多少

### 4.3 运行时 recipe

```csharp
[System.Serializable]
public class RuntimeJudgeRecipe
{
    public string recipeId;
    public string displayName;
    public List<JudgeRequirementEntry> requirements = new();
    public bool rejectUnexpectedTypes;
}
```

这个结构最终会被交给 judge。

---

## 5. 生成算法建议

这里推荐的不是“纯随机”，而是“受约束的随机”。

否则会出现几个常见问题：

- recipe 总数过大，玩家一轮做不完
- 某类型被随机出 0 个，但设计上你其实希望它经常出现
- 某些组合虽然随机合法，但不符合你们玩法节奏

### 5.1 目标

生成算法需要满足两层条件：

1. 硬约束
2. 随机分布

硬约束必须始终满足，例如：

- 总数在 `minTotalCount ~ maxTotalCount` 内
- 必选类型必须出现
- 单类数量不能超过该类 `maxCount`

随机分布才是在硬约束下做变化，例如：

- 今天这一轮是 `Pancake 2 + Fruit 1`
- 下一轮是 `Pancake 1 + Cream 1 + Topping 2`

### 5.2 推荐生成流程

建议生成流程按下面步骤走：

1. 初始化空 recipe
2. 先把所有 `required = true` 的类型加入 recipe
3. 每个必选类型至少先放入 `minCount`
4. 计算当前总数
5. 随机决定本轮目标总数 `targetTotal`
6. 在允许的类型中按权重继续补齐，直到达到 `targetTotal`
7. 每次补齐时检查该类型是否超过 `maxCount`
8. 如果补不下去或约束冲突，整轮重试
9. 成功后输出 `RuntimeJudgeRecipe`

### 5.3 为什么要做“重试”

因为多个约束放在一起后，某些随机路径会走进死路。

例如：

- `targetTotal = 5`
- 两个必选类型已经占掉 4 个
- 剩下某个类型 `maxCount` 也被占满

这种情况下继续补齐就不可能成功。

最简单稳定的做法不是写一堆复杂回溯，而是：

- 最多重试 20~50 次
- 成功一次就返回
- 如果始终失败，走兜底策略

### 5.4 兜底策略

建议准备一个简单兜底：

1. 直接用必选项 + 最小数量组成 recipe
2. 或者回退到一份固定默认 recipe

这样不会因为某次生成失败导致整轮卡死。

---

## 6. TriggerBoxJudge 需要怎样改

当前 `TriggerBoxJudge` 已经支持读取固定配置。为了接运行时 recipe，建议做一个“运行时覆盖层”。

推荐做法不是删除现有 `recipeConfig`，而是增加一个 runtime override。

例如：

```csharp
private RuntimeJudgeRecipe runtimeRecipe;

public void SetRuntimeRecipe(RuntimeJudgeRecipe recipe)
{
    runtimeRecipe = recipe;
}

public void ClearRuntimeRecipe()
{
    runtimeRecipe = null;
}
```

然后把当前生效逻辑改成：

1. 如果存在 `runtimeRecipe`，优先使用它
2. 否则使用 Inspector 上挂的 `recipeConfig`
3. 再不行才回退 legacy 数据

这样有几个好处：

- 不会破坏你现在的固定配置调试方式
- 运行时随机 recipe 能无缝接进现有 judge
- 场景里不需要为每一轮动态创建资产

### 6.1 建议抽出统一的“活动 recipe”

可以把 judge 内部读取 requirement 的逻辑统一成一个入口，例如：

```csharp
private IReadOnlyList<JudgeRequirementEntry> ActiveRequirements
{
    get
    {
        if (runtimeRecipe != null) return runtimeRecipe.requirements;
        if (recipeConfig != null) return recipeConfig.Requirements;
        return legacyRequirements;
    }
}
```

对应的 `rejectUnexpectedTypes` 也同理统一。

这样 `EvaluateRecipe()` 和 `BuildSummary()` 基本都不用重写，只要改成读活动 recipe 即可。

---

## 7. 谁来在合适时机生成 recipe

我建议把生成时机放在“回合开始”或“展示题目”阶段，而不是放在结算时。

结合现有 `ProcessManager` 的状态机，比较合理的接入点是：

### 方案 A：进入出题/准备状态时生成

如果 `State == 2` 是“展示题目 / 准备阶段”，那么流程可以是：

1. `ProcessManager` 切到 `State == 2`
2. `RecipeRoundController` 生成本轮 recipe
3. 把 recipe 发给 UI 展示
4. 把 recipe 发给 `TriggerBoxJudge`
5. 准备结束后进入制作阶段
6. 制作完成后调用 `JudgeNow()`

这是最自然的方案。

### 方案 B：每次进入制作状态前生成

如果你们没有单独的题目展示阶段，也可以在制作阶段开始前立刻生成。

但这种方式的问题是：

- 玩家可能来不及看题
- UI 和流程更容易打架

所以更推荐方案 A。

---

## 8. UI 层怎么接

随机 recipe 交给 judge 只是内部逻辑的一半，另一半是玩家必须知道这一轮目标是什么。

所以 runtime recipe 生成后，应该同时交给 UI。

建议 UI 只吃运行时 recipe 的只读数据，不直接读 judge。

推荐的数据流：

1. `RecipeRoundController` 生成 `RuntimeJudgeRecipe`
2. `RecipeRoundController` 调用 `judge.SetRuntimeRecipe(recipe)`
3. `RecipeRoundController` 调用 `ui.ShowRecipe(recipe)`
4. 回合结束时 UI 隐藏或刷新

这样 UI 和 Judge 都是下游消费者，不会互相依赖。

---

## 9. 推荐的最小可行版本

如果你现在要做第一版，我建议先不要一上来把系统做得很大，而是按下面顺序推进。

### 第一步：先支持“随机从规则里生成一份运行时 recipe”

新增：

- `JudgeRecipeGenerationConfig`
- `RuntimeJudgeRecipe`
- `RandomRecipeGenerator`

此时只做最基本规则：

- 总数范围
- 每类最小/最大数量
- 是否必选

### 第二步：让 `TriggerBoxJudge` 支持 runtime override

新增：

- `SetRuntimeRecipe()`
- `ClearRuntimeRecipe()`

并让 judge 的活动 requirement 从 runtime recipe 读取。

### 第三步：做一个回合控制器

新增：

- `RecipeRoundController`

负责：

- 开始时生成 recipe
- 发给 judge
- 发给 UI

### 第四步：再考虑难度和权重

第一版跑通后，再加：

- 不同阶段不同生成配置
- 权重
- 难度曲线
- 特殊 recipe 模板

这样改动风险最低。

---

## 10. 后续可扩展方向

如果未来玩法要继续扩展，这套结构可以自然支持下面这些能力：

### 10.1 难度递增

可以按轮次切换不同 `JudgeRecipeGenerationConfig`：

- 前几轮只出 1~2 种类型
- 后几轮允许 3~4 种类型
- 后期提高总数和复杂度

### 10.2 状态绑定

可以按 `ProcessManager.State` 决定当前使用哪一套生成规则。

例如：

- `State 3` 只生成 pancake 相关目标
- `State 4` 再加入 fruit
- `State 5` 再加入 topping / cream / syrup

### 10.3 模板池 + 受限随机混合

后期不一定完全用“纯规则生成”，也可以混合两种方式：

1. 一部分 recipe 来自手工模板池
2. 一部分 recipe 来自受约束随机生成

这样能同时保证：

- 一定的设计感
- 一定的新鲜感

### 10.4 固定种子复现

如果后面需要复现某轮题目，可以在生成器里保存随机种子：

- 调试更方便
- 便于复盘
- 便于做每日挑战

---

## 11. 我推荐的最终职责边界

最后把职责边界明确一下，后续实现时最好不要打破：

- `JudgeRecipeConfig`
  - 固定 recipe 资产
  - 用于手工配置和调试

- `JudgeRecipeGenerationConfig`
  - 随机生成的约束模板
  - 用于描述“允许生成什么”

- `RuntimeJudgeRecipe`
  - 本轮真实 recipe
  - 用于给 judge 和 UI 消费

- `RandomRecipeGenerator`
  - 负责从生成配置产出运行时 recipe

- `RecipeRoundController`
  - 负责本轮开始、下发 recipe、驱动 UI 和 judge

- `TriggerBoxJudge`
  - 只负责按当前活动 recipe 进行判定和评分

这个边界一旦立住，后面无论你们要做固定 recipe、随机 recipe、分难度 recipe，还是每日挑战，基本都不用再重写 judge 核心。

---

## 12. 一句话总结

最稳的实现思路不是让 `TriggerBoxJudge` 自己随机，而是：

1. 用一个生成配置描述随机约束
2. 用一个生成器在每轮开始时产出运行时 recipe
3. 用一个回合控制器把这份 runtime recipe 同时发给 judge 和 UI
4. judge 只负责按当前活动 recipe 扫描、判定、打分

这样改动最小，扩展性最好，也最符合你们现在已经拆出来的 judge 配置方向。
