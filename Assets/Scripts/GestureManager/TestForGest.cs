using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Leap;

public class TestForGest : MonoBehaviour
{
    public static TestForGest Instance { get; private set; }
    [SerializeField] HandSpawnController handSpawnController;

    // 1: 默认, 3: 放置(Spawn), 4: 手势/写(Gesture/Writing)
    public int state = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {

    }

    private void Update()
    {
        // 按下 1 切换到放置状态 (State 3: 放置松饼)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            state = 3;
            Debug.Log("[TestForGest] Switched to Placement Mode (State 3)");
        }

        // 按下 2 切换到写/手势识别状态 (State 4: 放果酱)
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            state = 4;
            Debug.Log("[TestForGest] Switched to Writing/Gesture Mode (State 4)");
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            handSpawnController.SpawnAtCurrentPoint();
        }

    }

    public bool IsPlacementMode()
    {
        return state == 3;
    }

    public bool IsGestureMode()
    {
        return state == 4 || state == 5;
    }
}
