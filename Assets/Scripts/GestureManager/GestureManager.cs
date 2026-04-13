using System.Collections.Generic;
using System.IO;
using System.Linq;
using GestureRecognition;
using Leap;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Route external game logic through GestureTemplateRecognizer instead of calling GestureManager directly.
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
    public string resourcesTemplatePath = "gesture_templates";

    [SerializeField] private string lastRecognizedLabel = "None";
    [SerializeField] private float lastMatchDistance = float.MaxValue;

    private readonly List<GesturePoint> currentStrokePoints = new List<GesturePoint>();
    private readonly List<GesturePoint> allPoints = new List<GesturePoint>();
    private readonly List<LineRenderer> activeLines = new List<LineRenderer>();
    private readonly List<Gesture> trainingSet = new List<Gesture>();

    private LineRenderer currentLine;
    private int strokeCounter;
    private bool wasPointingLastFrame;
    private bool isPointing;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string ResourcesAssetPath
    {
        get
        {
            string relativePath = resourcesTemplatePath.Replace('/', Path.DirectorySeparatorChar) + ".json";
            return Path.Combine(Application.dataPath, "Resources", relativePath);
        }
    }

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
        LoadTemplates();
    }

    private void OnEnable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame += OnUpdateFrame;
        }
    }

    private void OnDisable()
    {
        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame -= OnUpdateFrame;
        }
    }

    public void StartLogging()
    {
        if (!IsGestureModuleActive())
        {
            return;
        }

        isPointing = true;
    }

    public void StopLogging()
    {
        isPointing = false;
        UpdateFromFingers(Vector3.zero, false);
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
            UpdateFromFingers(Vector3.zero, false);
            return;
        }

        Vector3 tipPosition = indexFinger.TipPosition;
        UpdateFromFingers(tipPosition, isPointing);
    }

    public void UpdateFromFingers(Vector3 fingerPos, bool isPointingNow)
    {
        if (!IsGestureModuleActive())
        {
            isPointingNow = false;
        }

        if (isPointingNow && !wasPointingLastFrame)
        {
            StartStroke();
        }
        else if (!isPointingNow && wasPointingLastFrame)
        {
            EndStroke();
        }

        if (isPointingNow)
        {
            RecordPoint(fingerPos);
        }

        wasPointingLastFrame = isPointingNow;
    }

    private bool IsGestureModuleActive()
    {
        if (TestForGest.Instance != null)
        {
            return TestForGest.Instance.IsGestureMode();
        }

        return true;
    }

    private void StartStroke()
    {
        strokeCounter++;
        currentStrokePoints.Clear();

        if (linePrefab == null)
        {
            currentLine = GetComponent<LineRenderer>();
            if (currentLine == null)
            {
                return;
            }

            currentLine.positionCount = 0;
            return;
        }

        currentLine = Instantiate(linePrefab, transform);
        currentLine.positionCount = 0;
        activeLines.Add(currentLine);
    }

    private void EndStroke()
    {
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
            return;
        }

        GesturePoint point = new GesturePoint(worldPos, strokeCounter);
        currentStrokePoints.Add(point);
        allPoints.Add(point);

        if (currentLine != null)
        {
            currentLine.positionCount++;
            currentLine.SetPosition(currentLine.positionCount - 1, worldPos);
        }
    }

    public bool SaveTemplate()
    {
        if (allPoints.Count < 5)
        {
            return false;
        }

        trainingSet.Add(new Gesture(recordLabel, allPoints.ToArray()));
        SaveTemplatesToDisk();
        Clear();
        return true;
    }

    public void Recognize()
    {
        if (allPoints.Count < 5)
        {
            lastRecognizedLabel = "None";
            lastMatchDistance = float.MaxValue;
            return;
        }

        if (trainingSet.Count == 0)
        {
            lastRecognizedLabel = "None";
            lastMatchDistance = float.MaxValue;
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

        lastRecognizedLabel = bestMatch;
        lastMatchDistance = minDistance;
        Debug.Log($"识别结果: {bestMatch} (匹配距离: {minDistance})");
    }

    public string RecognizeLabel()
    {
        Recognize();
        return lastRecognizedLabel;
    }

    public string GetLastRecognizedLabel()
    {
        return lastRecognizedLabel;
    }

    public float GetLastMatchDistance()
    {
        return lastMatchDistance;
    }

    public void ClearDrawing()
    {
        Clear();
    }

    private void SaveTemplatesToDisk()
    {
        GestureTemplateStore store = BuildTemplateStore();
        string json = JsonUtility.ToJson(store, true);

        File.WriteAllText(SavePath, json);

#if UNITY_EDITOR
        string resourcesDirectory = Path.GetDirectoryName(ResourcesAssetPath);
        if (!string.IsNullOrEmpty(resourcesDirectory) && !Directory.Exists(resourcesDirectory))
        {
            Directory.CreateDirectory(resourcesDirectory);
        }

        File.WriteAllText(ResourcesAssetPath, json);
        AssetDatabase.Refresh();
#endif
    }

    private GestureTemplateStore BuildTemplateStore()
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

        return store;
    }

    private void LoadTemplates()
    {
        trainingSet.Clear();

        string json = LoadTemplatesJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        GestureTemplateStore store = JsonUtility.FromJson<GestureTemplateStore>(json);
        if (store == null || store.templates == null)
        {
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
    }

    private string LoadTemplatesJson()
    {
        TextAsset resourceAsset = Resources.Load<TextAsset>(resourcesTemplatePath);
        if (resourceAsset != null && !string.IsNullOrWhiteSpace(resourceAsset.text))
        {
            return resourceAsset.text;
        }

        return string.Empty;
    }

    private void Clear()
    {
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
