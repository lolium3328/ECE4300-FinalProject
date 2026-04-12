using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class TestForGest : MonoBehaviour
{
    private int internalState = 1;

    public static TestForGest Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {
        // 按下数字键 1 切换到放置模式 (Internal State 3)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            internalState = 3;
            Debug.Log("[TestForGest] Switched to Placement Mode (State 3)");
        }

        // 按下数字键 2 切换到手势识别模式 (Internal State 4)
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            internalState = 4;
            Debug.Log("[TestForGest] Switched to Gesture Recognition Mode (State 4)");
        }
    }

    public bool IsGestureRecognitionMode()
    {
        return internalState == 4 || internalState == 5;
    }

    public bool IsPlacementMode()
    {
        return internalState == 3;
    }
}