# Gesture 到 Spawn 的完整数据流

## 当前实现状态

这条链路现在已经具备完整的数据传递能力：

1. `GestureManager` 负责识别并保存最后一次标签
2. `GestureSpawnSelector` 负责把标签映射成 prefab
3. `HandSpawnController` 负责接收 prefab 并用于后续生成

注意：

- 现在链路已经完整
- 但是否“自动触发”，取决于你在场景里把哪个事件绑定到 `GestureSpawnSelector`

---

## 代码位置

- `Assets/Scripts/GestureManager/GestureManager.cs`
- `Assets/Scripts/GestureManager/GestureSpawnSelector.cs`
- `Assets/Scripts/GestureManager/HandSpawnController.cs`
- `Assets/Scripts/PancakeManager/GestureTemplateRecognizer.cs`

---

## 数据流分解

### 1. GestureManager 产生识别结果

`GestureManager.Recognize()` 会对当前笔迹和模板做匹配，并得到：

```csharp
string bestMatch
```

识别完成后，它会把结果写入内部字段：

```csharp
lastRecognizedLabel = bestMatch;
lastMatchDistance = minDistance;
```

现在这个结果可以通过以下方式读取：

```csharp
gestureManager.LastRecognizedLabel
gestureManager.GetLastRecognizedLabel()
```

---

### 2. GestureTemplateRecognizer 作为对外入口

项目里更推荐外部逻辑通过 `GestureTemplateRecognizer` 来取识别结果，而不是直接操控 `GestureManager`。

可用入口有两个：

```csharp
recognizer.RecognizeLabel()
recognizer.GetLastRecognizedLabel()
```

区别是：

- `RecognizeLabel()` 会先执行一次识别，再返回标签
- `GetLastRecognizedLabel()` 只读取最近一次识别结果，不会重新识别

---

### 3. GestureSpawnSelector 负责桥接

`GestureSpawnSelector.cs` 是这次新增的中间层，核心职责只有一件事：

- 把“识别结果字符串”转换成“要生成的 prefab”

它内部使用一个 Inspector 可配置映射：

```csharp
[System.Serializable]
public class GesturePrefabMapping
{
    public string gestureLabel;
    public GameObject prefab;
}
```

然后在 `GestureSpawnSelector` 中维护：

```csharp
public List<GesturePrefabMapping> mappings;
```

例如可以配置：

- `"1"` -> `pancake`
- `"2"` -> `strawberry`
- `"3"` -> `cylinder`

---

### 4. GestureSpawnSelector 的两个主要入口

#### `RecognizeAndApply()`

这个方法会一步完成整条链路：

1. 调用 `recognizer.RecognizeLabel()`
2. 拿到识别标签
3. 查找映射表
4. 把匹配到的 prefab 传给 `HandSpawnController`

示意：

```csharp
selector.RecognizeAndApply();
```

适合直接挂在：

- 按钮事件
- UnityEvent
- 识别完成后的流程事件

#### `ApplyLastRecognizedGesture()`

这个方法不会重新识别，只会取最近一次结果并应用：

```csharp
selector.ApplyLastRecognizedGesture();
```

适合这种流程：

1. 别的脚本先调用 `Recognize()`
2. 之后再单独调用 `ApplyLastRecognizedGesture()`

---

### 5. HandSpawnController 接收 prefab

当 `GestureSpawnSelector` 找到目标 prefab 后，会调用：

```csharp
handSpawnController.SetPrefabToSpawn(targetPrefab);
```

这个方法现在会：

1. 更新 `prefabToSpawn`
2. 必要时同步 `previewPrefab`
3. 刷新预览实例

所以后面真正执行：

```csharp
HandSpawnController.SpawnAtCurrentPoint()
```

时，生成出来的就是新选择的物体。

---

## 现在这条链路的真实传输过程

完整过程如下：

1. 用户完成一次手势输入
2. `GestureTemplateRecognizer` 执行识别
3. `GestureManager` 得到 `bestMatch`
4. `GestureManager` 保存到 `lastRecognizedLabel`
5. `GestureSpawnSelector` 读取该标签
6. `GestureSpawnSelector` 在 `mappings` 中找到对应 prefab
7. `GestureSpawnSelector` 调用 `HandSpawnController.SetPrefabToSpawn(...)`
8. 之后用户触发 Spawn 时，生成的就是映射后的 prefab

---

## 场景中如何使用

### Inspector 需要配置的引用

给 `GestureSpawnSelector` 挂上脚本后，至少配置：

1. `Recognizer`
   - 指向场景中的 `GestureTemplateRecognizer`
2. `Hand Spawn Controller`
   - 指向场景中的 `HandSpawnController`
3. `Mappings`
   - 配置标签与 prefab 的对应关系

---

### 推荐接法 A：一步式

如果你想让某个按钮或事件直接完成“识别并切换物体”，绑定：

```csharp
GestureSpawnSelector.RecognizeAndApply()
```

---

### 推荐接法 B：两步式

如果你想把“识别”和“应用”拆开控制：

1. 先调用：

```csharp
GestureTemplateRecognizer.Recognize()
```

2. 再调用：

```csharp
GestureSpawnSelector.ApplyLastRecognizedGesture()
```

---

## 已实现的附加行为

`GestureSpawnSelector` 还支持以下行为：

- `ignoreNoneLabel`
  - 当识别结果为 `"None"` 时，可以选择直接忽略
- `useFallbackWhenNoMatch`
  - 当映射表中找不到标签时，可以切到默认 prefab
- `fallbackPrefab`
  - 默认回退使用的 prefab
- `lastAppliedLabel`
  - 记录最近一次被应用的标签，便于调试
- `lastAppliedPrefab`
  - 记录最近一次被应用的 prefab，便于调试

---

## 结论

现在这条链路已经不是“只差中间层”，而是：

- 识别结果能产出
- 识别结果能读取
- 标签能映射成 prefab
- prefab 能传给 `HandSpawnController`
- 预览和实际生成物体能保持同步

后面如果你要继续扩展，下一步更合理的是把 `GestureSpawnSelector.RecognizeAndApply()` 接进你现在的按钮或状态机事件里，而不是再往 `GestureManager` 本体里塞逻辑。
