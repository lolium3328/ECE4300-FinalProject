using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestForGest : MonoBehaviour
{
    //测试放置模式用
    public static TestForGest Instance { get; private set; }
    [SerializeField] HandSpawnController handSpawnController;
    [SerializeField] GestureSpawnSelector gestureSpawnSelector;

    // 1: 默认, 3: 放置(Spawn), 4: 手势/写(Gesture/Writing)
    private int state = 1;

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
            state = 1;
            Debug.Log("[TestForGest] Switched to Placement Mode (State 3)");
        }

        // 按下 2 切换到写/手势识别状态 (State 4: 放果酱)
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            state = 2;
            Debug.Log("[TestForGest] Switched to Writing/Gesture Mode (State 4)");
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            handSpawnController.SpawnAtCurrentPoint();
        }

    }

    public bool IsPlacementMode()
    {
        return state == 1;
    }

    public bool IsGestureMode()
    {
        return state == 2;
    }
}
