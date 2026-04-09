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
        isPointing = true;
        // Debug.Log("开始追踪食指位置");
    }

    public void StopLogging()
    {
        isPointing = false;
        NotifyGestureManagerStopped();
        // Debug.Log("停止追踪");
    }

    private void OnUpdateFrame(Frame frame)
    {
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

        if (trailRenderer != null)
        {
            trailRenderer.transform.position = tipPosition;
        }

        if (gestureManager != null)
        {
            gestureManager.UpdateFromFingers(tipPosition, isPointing);
        }

        if (isPointing)
        {
            //Debug.Log($"Index Finger Tip Position: {tipPosition}");
        }
    }

    private void NotifyGestureManagerStopped()
    {
        if (gestureManager != null)
        {
            gestureManager.UpdateFromFingers(Vector3.zero, false);
        }
    }
}
