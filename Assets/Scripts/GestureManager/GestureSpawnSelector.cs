using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GesturePrefabMapping
{
    public string gestureLabel;
    public GameObject prefab;
}

public class GestureSpawnSelector : MonoBehaviour
{
    [Header("References")]
    public GestureTemplateRecognizer recognizer;
    public HandSpawnController handSpawnController;
    public CreamSurfacePlacementTester jamPlacementController;

    [Header("Mappings")]
    public List<GesturePrefabMapping> mappings = new List<GesturePrefabMapping>();
    public List<GesturePrefabMapping> jamMappings = new List<GesturePrefabMapping>();

    [Header("Options")]
    public bool ignoreNoneLabel = true;
    public bool useFallbackWhenNoMatch = false;
    public GameObject fallbackPrefab;       //可选的预设，当没有找到匹配的标签时使用
    public bool logSelection = true;       //是否在控制台输出每次应用的标签和预设信息，便于调试和验证识别结果

    [Header("Debug")]
    [SerializeField] private string lastAppliedLabel = "None";
    [SerializeField] private GameObject lastAppliedPrefab;

    public string LastAppliedLabel => lastAppliedLabel;
    public GameObject LastAppliedPrefab => lastAppliedPrefab;

    private void Reset()
    {
        if (recognizer == null)
        {
            recognizer = FindObjectOfType<GestureTemplateRecognizer>();
        }

        if (handSpawnController == null)
        {
            handSpawnController = FindObjectOfType<HandSpawnController>();
        }

        if (jamPlacementController == null)
        {
            jamPlacementController = FindObjectOfType<CreamSurfacePlacementTester>();
        }
    }

    public bool RecognizeAndApply()
    {
        if (ProcessManager.Instance != null && !ProcessManager.Instance.IsGestureMode())
        {
            if (logSelection)
            {
                Debug.Log("[GestureSpawnSelector] Ignored gesture selection because current mode is not gesture mode.");
            }

            return false;
        }

        if (recognizer == null)
        {
            Debug.LogWarning("[GestureSpawnSelector] Recognizer reference is missing.");
            return false;
        }

        string label = recognizer.RecognizeLabel();
        return ApplyRecognizedLabel(label);
    }

    // UnityEvent in the Inspector only lists void methods, so provide a wrapper
    // for direct binding from buttons / pose events.
    public void RecognizeAndApplyEvent()
    {
        RecognizeAndApply();
    }

    public bool ApplyLastRecognizedGesture()
    {
        if (ProcessManager.Instance != null && !ProcessManager.Instance.IsGestureMode())
        {
            if (logSelection)
            {
                Debug.Log("[GestureSpawnSelector] Ignored last gesture selection because current mode is not gesture mode.");
            }

            return false;
        }

        if (recognizer == null)
        {
            Debug.LogWarning("[GestureSpawnSelector] Recognizer reference is missing.");
            return false;
        }

        string label = recognizer.GetLastRecognizedLabel();
        return ApplyRecognizedLabel(label);
    }

    // UnityEvent in the Inspector only lists void methods, so provide a wrapper
    // for direct binding from buttons / pose events.
    public void ApplyLastRecognizedGestureEvent()
    {
        ApplyLastRecognizedGesture();
    }

    public bool ApplyRecognizedLabel(string label)
    {
        bool isJamSelection = ProcessManager.Instance != null && ProcessManager.Instance.State == 4;
        if (!isJamSelection && handSpawnController == null)
        {
            Debug.LogWarning("[GestureSpawnSelector] HandSpawnController reference is missing.");
            return false;
        }

        if (isJamSelection && jamPlacementController == null)
        {
            jamPlacementController = FindObjectOfType<CreamSurfacePlacementTester>(true);
            if (jamPlacementController == null)
            {
                Debug.LogWarning("[GestureSpawnSelector] Jam placement controller reference is missing.");
                return false;
            }
        }

        string normalizedLabel = NormalizeLabel(label);
        lastAppliedLabel = string.IsNullOrEmpty(normalizedLabel) ? "None" : normalizedLabel;

        if (string.IsNullOrEmpty(normalizedLabel))
        {
            lastAppliedPrefab = null;
            Debug.LogWarning("[GestureSpawnSelector] Recognized label is empty.");
            return false;
        }

        if (ignoreNoneLabel && string.Equals(normalizedLabel, "None", StringComparison.OrdinalIgnoreCase))
        {
            lastAppliedPrefab = null;

            if (logSelection)
            {
                Debug.Log("[GestureSpawnSelector] Applied label 'None'.");
            }

            return false;
        }

        if (TryGetMappedPrefab(normalizedLabel, isJamSelection, out GameObject mappedPrefab))
        {
            ApplyPrefabToCurrentStage(mappedPrefab, isJamSelection);
            lastAppliedPrefab = mappedPrefab;
            ClearGestureDrawing();
            if (ProcessManager.Instance != null)   
            {
                ProcessManager.Instance.SetPrefabToSpawnDone();     //发信号预设完成
            }

            if (logSelection)
            {
                Debug.Log($"[GestureSpawnSelector] Applied label '{normalizedLabel}' to prefab '{mappedPrefab.name}' for {(isJamSelection ? "jam" : "hand spawn")}.");
            }

            return true;
        }

        if (useFallbackWhenNoMatch && fallbackPrefab != null)      //如果没有找到匹配的标签，并且启用了使用预设作为后备选项，则应用后备预设
        {
            ApplyPrefabToCurrentStage(fallbackPrefab, isJamSelection);
            lastAppliedPrefab = fallbackPrefab;
            ClearGestureDrawing();
            if (ProcessManager.Instance != null)
            {
                ProcessManager.Instance.SetPrefabToSpawnDone();     //发信号预设完成
            }

            if (logSelection)
            {
                Debug.Log($"[GestureSpawnSelector] No mapping for '{normalizedLabel}', fallback prefab '{fallbackPrefab.name}' was applied.");
            }

            return true;
        }

        lastAppliedPrefab = null;
        Debug.LogWarning($"[GestureSpawnSelector] No prefab mapping found for label '{normalizedLabel}'.");
        return false;
    }

    private void ApplyPrefabToCurrentStage(GameObject prefab, bool isJamSelection)
    {
        if (isJamSelection)
        {
            jamPlacementController.SetPrefabToSpawn(prefab);
            return;
        }

        handSpawnController.SetPrefabToSpawn(prefab);
    }

    private void ClearGestureDrawing()
    {
        if (recognizer != null)
        {
            recognizer.ClearDrawing();
        }
    }

    private bool TryGetMappedPrefab(string label, bool isJamSelection, out GameObject prefab)
    {
        List<GesturePrefabMapping> activeMappings = isJamSelection ? jamMappings : mappings;

        foreach (GesturePrefabMapping mapping in activeMappings)
        {
            if (mapping == null || mapping.prefab == null)
            {
                continue;
            }

            string mappedLabel = NormalizeLabel(mapping.gestureLabel);
            if (!string.Equals(mappedLabel, label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            prefab = mapping.prefab;
            return true;
        }

        if (isJamSelection && jamMappings.Count == 0 && jamPlacementController != null)
        {
            prefab = jamPlacementController.CurrentPrefabToSpawn;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    private static string NormalizeLabel(string label)
    {
        return string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
    }
}
