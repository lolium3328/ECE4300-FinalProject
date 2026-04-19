# GadingManager 文件说明

说明：这里对应的是 `Assets/Scripts/GadingManager/` 目录，只说明 `.cs` 文件。

## 1. 目录里有什么文件

当前 `GadingManager` 目录下有 5 个脚本文件：

1. `JudgeRecipeGenerationConfig.cs`
2. `RuntimeJudgeRecipe.cs`
3. `RandomRecipeGenerator.cs`
4. `RecipeRoundController.cs`
5. `TriggerBoxJudge.cs`

它们可以分成三层：

- 静态生成规则层：`JudgeRecipeGenerationConfig`
- 运行时数据与生成层：`RuntimeJudgeRecipe`、`RandomRecipeGenerator`
- 调度与判定层：`RecipeRoundController`、`TriggerBoxJudge`

---

## 2. 每个文件的作用

## 2.1 `JudgeRecipeGenerationConfig.cs`

这个文件定义的是“随机生成 recipe 的约束模板”。

里面有两个核心内容：

- `RecipeGenerationRule`
  - 描述某个 `PrefabType` 在随机生成时的规则
  - 包含：
    - `prefabType`
    - `required`
    - `minCount`
    - `maxCount`
    - `weight`

- `JudgeRecipeGenerationConfig`
  - 是一个 `ScriptableObject`
  - 描述整轮随机生成时的全局约束
  - 包含：
    - `displayNamePrefix`
    - `minTotalCount`
    - `maxTotalCount`
    - `rejectUnexpectedTypes`
    - `maxGenerationAttempts`
    - `rules`

它的作用不是直接给 judge 判题，而是告诉生成器“允许出什么题、题目复杂度在什么范围”。

---

## 2.2 `RuntimeJudgeRecipe.cs`

这个文件定义的是“本轮实际生成出来的 recipe 数据”。

它还包含 `JudgeRequirementEntry`，这是最基础的一条需求：

- `PrefabType`
- `requiredCount`

`RuntimeJudgeRecipe` 本身的核心字段有：

- `recipeId`
- `displayName`
- `rejectUnexpectedTypes`
- `requirements`

它是运行时数据，不是资产文件。

它的作用是：

- 承载当前这一轮真正要判的 recipe
- 作为 `RandomRecipeGenerator` 的输出
- 作为 `TriggerBoxJudge` 的运行时输入

它还提供两个辅助能力：

- `Clone()`
  - 避免不同脚本直接共享同一份可变数据
- `BuildSummary()`
  - 生成文本摘要，方便在 Inspector 或日志里查看

---

## 2.3 `RandomRecipeGenerator.cs`

这个文件是“随机 recipe 生成器”。

它是一个静态工具类，核心入口是：

- `TryGenerate(JudgeRecipeGenerationConfig config, out RuntimeJudgeRecipe recipe, out string failureReason)`

它做的事情分成四步：

1. 校验生成配置是否合法
2. 先放入所有必选项
3. 在总数范围和单类上下限内按权重补齐
4. 失败时重试，必要时走 fallback

它的输出是：

- 一份 `RuntimeJudgeRecipe`
- 或者失败信息

它只负责“出题”，不负责判题，也不负责流程调度。

---

## 2.4 `RecipeRoundController.cs`

这个文件是“最小回合控制器”。

它当前的职责比较单纯：把“随机生成”接到“judge”上。

核心引用：

- `targetJudge`
- `generationConfig`

核心方法：

- `GenerateAndApplyRecipe()`
  - 调用 `RandomRecipeGenerator`
  - 生成一份 `RuntimeJudgeRecipe`
  - 交给 `TriggerBoxJudge.SetRuntimeRecipe()`

- `GenerateApplyAndJudge()`
  - 先生成并下发 recipe
  - 再立刻调用 `JudgeNow()`

- `ClearRuntimeRecipe()`
  - 清掉当前回合的运行时 recipe

当前版本里，它还没有真正和 UI 或完整状态机深度绑定，但最小闭环已经打通了。

---

## 2.5 `TriggerBoxJudge.cs`

这个文件是“真正负责判定和打分”的核心脚本。

它不负责生成 recipe，只负责消费当前活动 recipe，然后扫描判定区。

它现在只接受一种 recipe 来源：

1. `runtimeRecipe`

也就是说：

- `RecipeRoundController` 先把本轮生成好的 runtime recipe 交给它
- `TriggerBoxJudge` 再按这份 runtime recipe 去判
- 如果没有 runtime recipe，它会直接报错提示，而不会继续回退到旧配置

它的主要工作流程是：

1. 找到判定用的 `BoxCollider`
2. 用 `Physics.OverlapBox` 扫描盒子里的可判定对象
3. 通过 `PrefabIdentity` 识别物体类型
4. 统计各类型数量
5. 按当前 runtime recipe 做数量匹配判断
6. 按物体围绕锚点的对齐程度计算同心度分数
7. 输出最终 summary、分数和匹配结果

所以整个目录里，`TriggerBoxJudge` 是最后真正执行判定的“裁判”。

---

## 3. 数据流怎么流动

当前最重要的数据流只有一条：随机 recipe 流。

## 3.1 随机 recipe 流

流程如下：

1. 设计者创建 `JudgeRecipeGenerationConfig`
2. 在里面配置：
   - 可出现的 `PrefabType`
   - 每种类型的最小/最大数量
   - 是否必选
   - 权重
   - 总数范围
3. `RecipeRoundController` 持有这份生成配置
4. 调用 `RecipeRoundController.GenerateAndApplyRecipe()`
5. `RecipeRoundController` 调用 `RandomRecipeGenerator.TryGenerate(...)`
6. `RandomRecipeGenerator` 生成一份 `RuntimeJudgeRecipe`
7. `RecipeRoundController` 把这份 recipe 传给 `TriggerBoxJudge.SetRuntimeRecipe(...)`
8. 后续调用 `TriggerBoxJudge.JudgeNow()`
9. `TriggerBoxJudge` 读取 `runtimeRecipe`
10. judge 完成扫描、比较和打分

这条流里，真正流动的数据对象是：

- 输入：`JudgeRecipeGenerationConfig`
- 中间结果：`RuntimeJudgeRecipe`
- 最终消费：`TriggerBoxJudge`

---

## 4. 可以把它理解成什么结构

如果用一句话概括这个目录的结构，可以理解成：

- `JudgeRecipeGenerationConfig`：出题规则
- `RuntimeJudgeRecipe`：本轮题目
- `RandomRecipeGenerator`：出题器
- `RecipeRoundController`：发题器
- `TriggerBoxJudge`：判题器

也就是：

“先出题，再发题，最后判题”

---

## 5. 当前版本的边界

当前 `GadingManager` 已经完成的是：

- 随机 recipe 生成
- 把随机 recipe 下发给 judge
- judge 按本轮 recipe 判定

当前还没完全接上的部分是：

- UI 展示当前 recipe
- 和 `ProcessManager` 的状态自动联动
- 难度分层和多套生成配置自动切换

所以现在这套目录更适合被理解为：

- judge 核心已成型
- 随机 recipe 最小闭环已打通
- 还可以继续向完整回合系统扩展
