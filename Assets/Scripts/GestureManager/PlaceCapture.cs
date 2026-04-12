using Leap;
using UnityEngine;

public class FingerPainter : MonoBehaviour
{
    public LeapProvider leapProvider;
    public TrailRenderer trailRenderer;
    public GestureManager gestureManager;
    public Chirality handType = Chirality.Right;

    private bool isPointing;

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
        NotifyGestureManagerStopped();
    }

    private void OnUpdateFrame(Frame frame)
    {
        if (!IsGestureModuleActive())
        {
            isPointing = false;
            NotifyGestureManagerStopped();
            return;
        }

        Hand hand = frame.GetHand(handType);
        if (hand == null)
        {
            NotifyGestureManagerStopped();
            return;
        }

        UpdateHand(hand);
    }

    private void UpdateHand(Hand hand)
    {
        Finger indexFinger = hand.Index;
        if (indexFinger == null)
        {
            NotifyGestureManagerStopped();
            return;
        }

        Vector3 tipPosition = indexFinger.TipPosition;

        // 在控制台输出指尖的实时坐标
        Debug.Log($"[FingerPainter] Tip Position: {tipPosition}");

        if (trailRenderer != null)
        {
            trailRenderer.transform.position = tipPosition;
        }

        if (gestureManager != null)
        {
            gestureManager.UpdateFromFingers(tipPosition, isPointing);
        }
    }

    private void NotifyGestureManagerStopped()
    {
        if (gestureManager != null)
        {
            gestureManager.UpdateFromFingers(Vector3.zero, false);
        }
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
}
