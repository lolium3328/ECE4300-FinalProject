using System.Collections.Generic;
using System.IO;
using System.Linq;
using GestureRecognition;
using Leap;
using UnityEngine;

/*
使用方法：
1. 把本脚本挂到一个空物体上，例如 GestureManager。
2. 在 Inspector 里给 linePrefab 赋值一个 LineRenderer 预制体，用来显示手势轨迹。
3. 再把 PlaceCapture.cs 所在物体上的 gestureManager 字段拖到这个物体。
4. 运行后，当 PlaceCapture 那边把 isPointing 置为 true 时，会自动开始记录食指尖轨迹。
5. 当 isPointing 变回 false 时，会自动结束当前一笔。
6. 按 R：把当前轨迹保存为模板并写入本地 json；按 Space：识别当前轨迹；按 C：清空当前轨迹。
7. recordLabel 是当前要保存的模板标签，保存前可在 Inspector 里修改。
8. 模板文件默认保存在 Application.persistentDataPath 下的 gesture_templates.json。
*/
public class GestureManager : MonoBehaviour
{
    [Header("Settings")]
    public float minPointDistance = 0.05f;
    public string recordLabel = "6";

    [Header("Visuals")]
    public LineRenderer linePrefab;

    [Header("Leap Input")]
    public LeapProvider leapProvider;
    public Chirality handType = Chirality.Right;

    [Header("Persistence")]
    public string saveFileName = "gesture_templates.json";

    private readonly List<GesturePoint> currentStrokePoints = new List<GesturePoint>();
    private readonly List<GesturePoint> allPoints = new List<GesturePoint>();
    private readonly List<LineRenderer> activeLines = new List<LineRenderer>();
    private readonly List<Gesture> trainingSet = new List<Gesture>();

    private LineRenderer currentLine;
    private int strokeCounter;
    private bool wasPointingLastFrame;
    private bool isPointing;
    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    [System.Serializable]
    private class GestureTemplateData
    {
        public string name;
        public GesturePoint[] points;
    }

    [System.Serializable]
    private class GestureTemplateStore
    {
        public List<GestureTemplateData> templates = new List<GestureTemplateData>();
    }

    private void Awake()
    {
        // Debug.Log($"[GestureManager] Awake on {name}. linePrefab={(linePrefab != null ? linePrefab.name : "NULL")}. savePath={SavePath}");
        LoadTemplates();
    }

    private void OnEnable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame += OnUpdateFrame;
            // Debug.Log($"[GestureManager] Subscribed to LeapProvider on {name}");
        }
        else
        {
            // Debug.LogWarning($"[GestureManager] leapProvider is NULL on {name}");
        }
    }

    private void OnDisable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame -= OnUpdateFrame;
            // Debug.Log($"[GestureManager] Unsubscribed from LeapProvider on {name}");
        }
    }

    private void Update()
    {
        if (!IsGestureModuleActive())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            SaveTemplate();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Recognize();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Clear();
        }
    }

    public void StartLogging()
    {
        if (!IsGestureModuleActive())
        {
            return;
        }

        isPointing = true;
        // Debug.Log("[GestureManager] StartLogging called. isPointing=true");
    }

    public void StopLogging()
    {
        isPointing = false;
        UpdateFromFingers(Vector3.zero, false);
        // Debug.Log("[GestureManager] StopLogging called. isPointing=false");
    }

    private void OnUpdateFrame(Frame frame)
    {
        if (!IsGestureModuleActive())
        {
            if (isPointing || wasPointingLastFrame)
            {
                isPointing = false;
                UpdateFromFingers(Vector3.zero, false);
            }

            return;
        }

        Hand hand = frame.GetHand(handType);
        if (hand == null)
        {
            if (wasPointingLastFrame)
            {
                // Debug.Log("[GestureManager] No hand found. Ending current stroke.");
            }

            UpdateFromFingers(Vector3.zero, false);
            return;
        }

        OnUpdateHand(hand);
    }

    private void OnUpdateHand(Hand hand)
    {
        Finger indexFinger = hand.Index;
        if (indexFinger == null)
        {
            if (wasPointingLastFrame)
            {
                // Debug.Log("[GestureManager] No index finger found. Ending current stroke.");
            }

            UpdateFromFingers(Vector3.zero, false);
            return;
        }

        Vector3 tipPosition = indexFinger.TipPosition;

        if (isPointing)
        {
            // Debug.Log($"[GestureManager] Read Leap tip position: {tipPosition}");
        }

        UpdateFromFingers(tipPosition, isPointing);
    }

    public void UpdateFromFingers(Vector3 fingerPos, bool isPointing)
    {
        if (!IsGestureModuleActive())
        {
            isPointing = false;
        }

        if (isPointing)
        {
            // Debug.Log($"[GestureManager] UpdateFromFingers received point={fingerPos}, wasPointingLastFrame={wasPointingLastFrame}");
        }

        if (isPointing && !wasPointingLastFrame)
        {
            StartStroke();
        }
        else if (!isPointing && wasPointingLastFrame)
        {
            EndStroke();
        }

        if (isPointing)
        {
            RecordPoint(fingerPos);
        }

        wasPointingLastFrame = isPointing;
    }

    private bool IsGestureModuleActive()
    {
        if (TestForGest.Instance != null)
        {
            return TestForGest.Instance.IsGestureRecognitionMode();
        }

        if (ProcessManager.Instance == null)
        {
            return true;
        }

        return ProcessManager.Instance.State == 4 || ProcessManager.Instance.State == 5;
    }

    private void StartStroke()
    {
        strokeCounter++;
        currentStrokePoints.Clear();
        // Debug.Log($"[GestureManager] StartStroke. strokeCounter={strokeCounter}, linePrefab={(linePrefab != null ? linePrefab.name : "NULL")}");

        if (linePrefab == null)
        {
            currentLine = GetComponent<LineRenderer>();
            if (currentLine == null)
            {
                // Debug.LogWarning("[GestureManager] No linePrefab and no local LineRenderer. Stroke will be recorded but no line will be displayed.");
                return;
            }

            currentLine.positionCount = 0;
            // Debug.Log($"[GestureManager] Reusing local LineRenderer: {currentLine.name}");
            return;
        }

        currentLine = Instantiate(linePrefab, transform);
        currentLine.positionCount = 0;
        activeLines.Add(currentLine);
        // Debug.Log($"[GestureManager] LineRenderer instantiated: {currentLine.name}. activeLines={activeLines.Count}");
    }

    private void EndStroke()
    {
        // Debug.Log($"[GestureManager] EndStroke. currentStrokePoints={currentStrokePoints.Count}, allPoints={allPoints.Count}");
        currentStrokePoints.Clear();
        currentLine = null;
    }

    private void RecordPoint(Vector3 worldPos)
    {
        bool isFirstPointInStroke = currentStrokePoints.Count == 0;
        bool isFarEnough =
            !isFirstPointInStroke &&
            Vector3.Distance(currentStrokePoints.Last().Pos, worldPos) > minPointDistance;

        if (!isFirstPointInStroke && !isFarEnough)
        {
            // Debug.Log($"[GestureManager] RecordPoint skipped. distance too small. currentStrokePoints={currentStrokePoints.Count}");
            return;
        }

        GesturePoint point = new GesturePoint(worldPos, strokeCounter);
        currentStrokePoints.Add(point);
        allPoints.Add(point);
        // Debug.Log($"[GestureManager] RecordPoint accepted. stroke={strokeCounter}, strokePoints={currentStrokePoints.Count}, allPoints={allPoints.Count}, pos={worldPos}");

        if (currentLine != null)
        {
            currentLine.positionCount++;
            currentLine.SetPosition(currentLine.positionCount - 1, worldPos);
            // Debug.Log($"[GestureManager] Line updated. positionCount={currentLine.positionCount}");
        }
        else
        {
            // Debug.LogWarning("[GestureManager] currentLine is NULL while recording. Points are being stored, but nothing is drawn.");
        }
    }

    private void SaveTemplate()
    {
        if (allPoints.Count < 5)
        {
            // Debug.LogWarning("点数不足，至少需要 5 个点才能保存模板。");
            return;
        }

        trainingSet.Add(new Gesture(recordLabel, allPoints.ToArray()));
        SaveTemplatesToDisk();
        int labelCount = trainingSet.Count(template => template.Name == recordLabel);
        // Debug.Log($"已保存模板: {recordLabel}。该标签当前共有 {labelCount} 条模板，模板库总数: {trainingSet.Count}。文件路径: {SavePath}");
        Clear();
    }

    private void Recognize()
    {
        if (allPoints.Count < 5)
        {
            // Debug.LogWarning("当前轨迹点数不足，无法识别。");
            return;
        }

        if (trainingSet.Count == 0)
        {
            // Debug.LogWarning("模板库为空，请先录制并保存模板。");
            return;
        }

        GesturePoint[] candidate = RecognizeUtils.Normalize(allPoints.ToArray());
        string bestMatch = "None";
        float minDistance = float.MaxValue;

        foreach (Gesture template in trainingSet)
        {
            float distance = RecognizeUtils.GreedyCloudMatch(candidate, template.Points);
            if (distance < minDistance)
            {
                minDistance = distance;
                bestMatch = template.Name;
            }
        }

        Debug.Log($"识别结果: {bestMatch} (匹配距离: {minDistance})");
    }

    private void SaveTemplatesToDisk()
    {
        GestureTemplateStore store = new GestureTemplateStore();

        foreach (Gesture template in trainingSet)
        {
            store.templates.Add(new GestureTemplateData
            {
                name = template.Name,
                points = template.Points
            });
        }

        string json = JsonUtility.ToJson(store, true);
        File.WriteAllText(SavePath, json);
        // Debug.Log($"[GestureManager] Templates saved to disk. count={store.templates.Count}, path={SavePath}");
    }

    private void LoadTemplates()
    {
        trainingSet.Clear();

        if (!File.Exists(SavePath))
        {
            // Debug.Log($"未找到手势模板文件，将使用空模板库。路径: {SavePath}");
            return;
        }

        string json = File.ReadAllText(SavePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            // Debug.LogWarning($"手势模板文件为空，将使用空模板库。路径: {SavePath}");
            return;
        }

        GestureTemplateStore store = JsonUtility.FromJson<GestureTemplateStore>(json);
        if (store == null || store.templates == null)
        {
            // Debug.LogWarning($"手势模板文件解析失败，将使用空模板库。路径: {SavePath}");
            return;
        }

        foreach (GestureTemplateData templateData in store.templates)
        {
            if (templateData == null || templateData.points == null || templateData.points.Length < 5)
            {
                continue;
            }

            trainingSet.Add(new Gesture(templateData.name, templateData.points));
        }

        // Debug.Log($"已加载 {trainingSet.Count} 条手势模板。路径: {SavePath}");
    }

    private void Clear()
    {
        // Debug.Log($"[GestureManager] Clear called. activeLines={activeLines.Count}, allPoints={allPoints.Count}");
        foreach (LineRenderer line in activeLines)
        {
            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }

        activeLines.Clear();
        currentStrokePoints.Clear();
        allPoints.Clear();
        currentLine = null;
        strokeCounter = 0;
        wasPointingLastFrame = false;
    }
}
