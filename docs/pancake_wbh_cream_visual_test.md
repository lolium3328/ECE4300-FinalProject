# Pancake_wbh 基础 Sphere 奶油生成测试方案

## 当前目标

先忽略材质，只用 Unity 最基础的 Sphere 做效果测试。

这一版只验证：

- 能不能在 `Pancake_wbh` 场景里控制生成位置。
- 能不能只生成在已有物体表面。
- 能不能用多个无碰撞 Sphere 组成一小团奶油。
- Sphere 之间的尺寸和位置是否能形成“堆叠体块”的感觉。

暂时不考虑：

- 奶油材质
- 自定义 shader
- metaball / SDF 融合
- 粒子系统
- 物理堆叠
- `TriggerBoxJudge`
- 主流程接入

## 测试场景结构

在 `Assets/Scenes/Pancake_wbh.unity` 里新建：

```text
CreamPlacementTest
├── CreamPlacementCursor
└── CreamSpawnedRoot
```

说明：

- `CreamPlacementTest`：测试控制器挂载点。
- `CreamPlacementCursor`：显示当前准备生成的位置。
- `CreamSpawnedRoot`：所有测试生成出来的奶油 cluster 都放到这里，方便清理。

## 需要的 prefab

### `CreamCluster_Basic.prefab`

建议路径：

```text
Assets/Prefabs/Cream/CreamCluster_Basic.prefab
```

结构：

```text
CreamCluster_Basic
└── runtime generated spheres
```

这个 prefab 本身可以先是一个空父物体。  
Sphere 不需要提前手动摆好，而是在生成时由测试脚本随机创建。

运行时创建出来的每个 Sphere：

```text
MeshFilter: Sphere
MeshRenderer: Unity 默认材质即可
Collider: 删除
Rigidbody: 不要
PrefabIdentity: 不要
```

第一版不要给 Sphere 加 Collider。  
它们只负责视觉，不参与碰撞和物理。

## Sphere 随机分布规则

父物体 `CreamCluster_Basic` 的位置作为放置中心点。

你需要的不是固定奶油造型，而是：

```text
中心位置生成得比较多
四周随机离散分布
越靠外数量越少
每个 Sphere 大小略有随机
```

推荐第一版参数：

```text
sphereCount = 24 - 40
clusterRadius = 0.09
centerBias = 2.0 - 3.0
sphereScale = 0.03
heightJitter = 0.0 - 0.018
```

## 中心密集、边缘稀疏的采样方式

不要用完全均匀的 `Random.insideUnitCircle * radius`，那样中心和边缘密度差不多。

用带 bias 的半径采样：

```csharp
float angle = Random.Range(0f, Mathf.PI * 2f);
float radius01 = Mathf.Pow(Random.value, centerBias);
float radius = radius01 * clusterRadius;

Vector2 offset = new Vector2(
    Mathf.Cos(angle),
    Mathf.Sin(angle)
) * radius;
```

解释：

```text
centerBias = 1    接近均匀分布
centerBias = 2    中心更多
centerBias = 3    中心非常密集
```

生成 local position：

```csharp
float y = Random.Range(0f, heightJitter);
Vector3 localPosition = new Vector3(offset.x, y, offset.y);
```

生成 scale：

```csharp
float scale = sphereScale;

// 稍微压扁 Y，让它们像落在表面的颗粒，而不是完整弹珠。
Vector3 localScale = new Vector3(scale, scale * 0.65f, scale);
```

## 推荐生成结果

生成后大概应该是：

```text
中心 8-15 个 Sphere 比较密集
中间区域 10-18 个 Sphere 随机分布
边缘少量 Sphere 离散散开
```

视觉上不是一个固定奶油花，而是一堆围绕中心散落的小球体。

## 生成控制脚本

建议新增脚本：

```text
Assets/Scripts/Cream/CreamSurfacePlacementTester.cs
```

这个脚本只用于 `Pancake_wbh` 测试。

职责：

- 用键盘移动一个 cursor。
- 从 cursor 上方向下 Raycast。
- 命中已有物体表面后，把 cursor 放到命中点。
- 按键生成 `CreamCluster_Basic.prefab`。
- 可选：清空已生成的 cluster。

## 脚本字段设计

```csharp
[Header("References")]
[SerializeField] private GameObject creamClusterPrefab;
[SerializeField] private Transform placementCursor;
[SerializeField] private Transform spawnParent;

[Header("Surface Raycast")]
[SerializeField] private LayerMask surfaceMask = ~0;
[SerializeField] private float rayStartHeight = 0.5f;
[SerializeField] private float rayDistance = 2f;
[SerializeField] private float surfaceOffset = 0.01f;
[SerializeField] private bool alignToSurfaceNormal = true;

[Header("Keyboard Controls")]
[SerializeField] private float moveSpeed = 0.35f;
[SerializeField] private KeyCode spawnKey = KeyCode.F;
[SerializeField] private KeyCode clearKey = KeyCode.Backspace;
```

## 键盘控制

```text
方向键 左/右 -> cursor 沿 X 移动
方向键 上/下 -> cursor 沿 Z 移动
F            -> 在当前表面位置生成 Sphere 奶油团
Backspace    -> 清空测试生成的奶油团
```

不使用 `S`，避免和现有 `HandSpawnController.SpawnAtCurrentPoint()` 冲突。

## 表面吸附逻辑

核心逻辑：

```text
1. cursorPosition 只在 XZ 平面移动。
2. 每帧从 cursorPosition 上方向下 Raycast。
3. 如果命中 surfaceMask 里的物体：
   placementCursor.position = hit.point + hit.normal * surfaceOffset
4. 按 F 时：
   Instantiate(CreamCluster_Basic, placementCursor.position, rotation)
```

伪代码：

```csharp
Vector3 rayOrigin = cursorPosition + Vector3.up * rayStartHeight;

bool hasHit = Physics.Raycast(
    rayOrigin,
    Vector3.down,
    out RaycastHit hit,
    rayDistance,
    surfaceMask,
    QueryTriggerInteraction.Ignore
);

if (hasHit)
{
    placementCursor.position = hit.point + hit.normal * surfaceOffset;

    if (alignToSurfaceNormal)
    {
        placementCursor.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
    }
}
```

生成：

```csharp
Quaternion rotation = alignToSurfaceNormal
    ? Quaternion.FromToRotation(Vector3.up, hit.normal)
    : Quaternion.identity;

Instantiate(creamClusterPrefab, placementCursor.position, rotation, spawnParent);
```

## Surface Mask 设置

为了保证只生成在现有物体表面，建议新建 Layer：

```text
CreamSurface
```

然后把允许生成奶油的物体设为这个 Layer，比如：

```text
pancake
plate
cutting board
```

`CreamSurfacePlacementTester.surfaceMask` 只勾选：

```text
CreamSurface
```

如果暂时不想建 Layer，可以先用 `Everything`，但可能 Raycast 到 TriggerBox 或其他不想要的物体。

## Cursor 设置

`CreamPlacementCursor` 可以用一个小 Sphere：

```text
GameObject: CreamPlacementCursor
Mesh: Sphere
Scale: (0.03, 0.03, 0.03)
Collider: 删除
Material: 默认材质即可
```

它只用来显示当前位置。

## Pancake_wbh 中挂载步骤

1. 打开：

```text
Assets/Scenes/Pancake_wbh.unity
```

2. 新建空物体：

```text
CreamPlacementTest
```

3. 新建子物体：

```text
CreamPlacementCursor
```

4. 新建空物体：

```text
CreamSpawnedRoot
```

5. 在 `CreamPlacementTest` 上挂：

```text
CreamSurfacePlacementTester
```

6. 设置 Inspector：

```text
creamClusterPrefab = CreamCluster_Basic.prefab
placementCursor = CreamPlacementCursor
spawnParent = CreamSpawnedRoot
surfaceMask = CreamSurface
rayStartHeight = 0.5
rayDistance = 2
surfaceOffset = 0.01
alignToSurfaceNormal = true
moveSpeed = 0.35
spawnKey = F
clearKey = Backspace
```

## 测试标准

进入 Play Mode 后：

```text
1. 用方向键移动 cursor。
2. cursor 应该贴在 pancake/盘子表面。
3. 按 F 后生成一团 Sphere。
4. 生成的 Sphere 不应该掉落、不应该被物理推开。
5. Sphere 应该在表面上，而不是浮空或穿太深。
6. Backspace 能清空测试生成物。
```

## 如果效果不对怎么调

### 奶油浮空

调小：

```text
surfaceOffset
```

或者降低 `CreamCluster_Basic` 里子 Sphere 的 local Y。

### 奶油穿进表面太多

调大：

```text
surfaceOffset
```

或者提高子 Sphere 的 local Y。

### 看起来太散

调小：

```text
clusterRadius
```

或者调大：

```text
centerBias
```

让更多 Sphere 靠近中心。

### 中心不够密集

调大：

```text
centerBias
sphereCount
```

### 边缘太空

调小：

```text
centerBias
```

或者调大：

```text
sphereCount
clusterRadius
```

### 看起来太像完整球

继续压扁 Y 轴：

```text
localScale.y 更小
```

## 后续再考虑

基础 Sphere 测试通过后，再考虑：

```text
1. 换奶油材质。
2. 根据这些 Sphere 的平均中心点和离散程度评分。
3. 接入键盘/手势正式选择。
4. 接入 TriggerBoxJudge。
```
