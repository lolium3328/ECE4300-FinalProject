using UnityEngine;

// Use this facade from gameplay logic instead of calling GestureManager directly.
public class GestureTemplateRecognizer : MonoBehaviour
{
    [Header("References")]
    public GestureManager gestureManager;

    [Header("Input Routing")]
    public bool enableKeyboardInput = true;
    public bool enableRecognizeInput = true;
    public bool enableSaveTemplateInput = true;
    public bool enableClearInput = true;

    [Header("Input Keys")]
    public KeyCode recognizeKey = KeyCode.R;
    public KeyCode saveTemplateKey = KeyCode.Space;
    public KeyCode clearKey = KeyCode.C;

    private void Update()
    {
        if (!enableKeyboardInput || gestureManager == null)
        {
            return;
        }

        if (enableSaveTemplateInput && Input.GetKeyDown(saveTemplateKey))
        {
            gestureManager.SaveTemplate();
        }

        if (enableRecognizeInput && Input.GetKeyDown(recognizeKey))
        {
            Debug.Log("Recognize input detected.");
            gestureManager.Recognize();
        }

        if (enableClearInput && Input.GetKeyDown(clearKey))
        {
            gestureManager.ClearDrawing();
        }
    }

    public void Recognize()
    {
        if (gestureManager == null || !enableRecognizeInput)
        {
            return;
        }

        gestureManager.Recognize();
    }

    public string RecognizeLabel()
    {
        if (gestureManager == null || !enableRecognizeInput)
        {
            return "None";
        }

        return gestureManager.RecognizeLabel();
    }

    public bool SaveTemplate()
    {
        if (gestureManager == null || !enableSaveTemplateInput)
        {
            return false;
        }

        return gestureManager.SaveTemplate();
    }

    public void ClearDrawing()
    {
        if (gestureManager == null || !enableClearInput)
        {
            return;
        }

        gestureManager.ClearDrawing();
    }

    public string GetLastRecognizedLabel()
    {
        if (gestureManager == null)
        {
            return "None";
        }

        return gestureManager.GetLastRecognizedLabel();
    }

    public float GetLastMatchDistance()
    {
        if (gestureManager == null)
        {
            return float.MaxValue;
        }

        return gestureManager.GetLastMatchDistance();
    }
}
