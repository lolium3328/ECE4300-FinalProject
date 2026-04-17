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

    [Header("Mappings")]
    public List<GesturePrefabMapping> mappings = new List<GesturePrefabMapping>();

    [Header("Options")]
    public bool ignoreNoneLabel = true;
    public bool useFallbackWhenNoMatch = false;
    public GameObject fallbackPrefab;
    public bool logSelection = true;

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
    }

    public bool RecognizeAndApply()
    {
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
        if (handSpawnController == null)
        {
            Debug.LogWarning("[GestureSpawnSelector] HandSpawnController reference is missing.");
            return false;
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
                Debug.Log("[GestureSpawnSelector] Ignored label 'None'.");
            }

            return false;
        }

        if (TryGetMappedPrefab(normalizedLabel, out GameObject mappedPrefab))
        {
            handSpawnController.SetPrefabToSpawn(mappedPrefab);
            lastAppliedPrefab = mappedPrefab;

            if (logSelection)
            {
                Debug.Log($"[GestureSpawnSelector] Applied label '{normalizedLabel}' to prefab '{mappedPrefab.name}'.");
            }

            return true;
        }

        if (useFallbackWhenNoMatch && fallbackPrefab != null)
        {
            handSpawnController.SetPrefabToSpawn(fallbackPrefab);
            lastAppliedPrefab = fallbackPrefab;

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

    private bool TryGetMappedPrefab(string label, out GameObject prefab)
    {
        foreach (GesturePrefabMapping mapping in mappings)
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

        prefab = null;
        return false;
    }

    private static string NormalizeLabel(string label)
    {
        return string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
    }
}
