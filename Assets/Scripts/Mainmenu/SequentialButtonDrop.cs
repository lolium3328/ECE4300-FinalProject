using System.Collections;
using UnityEngine;

public class SequentialButtonDrop : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform[] buttons;
    [SerializeField] private bool collectChildrenInOrder = false;
    [SerializeField] private bool playOnStart = true;

    [Header("Drop Motion")]
    [SerializeField] private bool useLocalPosition = true;
    [SerializeField] private Vector3 startOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private float firstDelay = 0.50f;
    [SerializeField] private float delayBetweenButtons = 0.60f;
    [SerializeField] private float dropDuration = 0.60f;
    [SerializeField] private AnimationCurve dropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Landing Feedback")]
    [SerializeField] private bool addSmallBounce = true;
    [SerializeField] private float bounceHeight = 0.16f;
    [SerializeField] private float bounceDuration = 0.12f;

    private Coroutine playRoutine;
    private Vector3[] targetPositions;

    private void Awake()
    {
        RefreshTargets();
        CacheCurrentPositions();
        MoveTargetsToDropStart();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    [ContextMenu("Play Button Drop")]
    public void Play()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        RefreshTargets();
        CacheCurrentPositions();
        MoveTargetsToDropStart();
        playRoutine = StartCoroutine(PlayDropSequence());
    }

    private void RefreshTargets()
    {
        if (buttons == null)
        {
            buttons = new Transform[0];
        }

        if (!collectChildrenInOrder)
        {
            return;
        }

        buttons = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            buttons[i] = transform.GetChild(i);
        }
    }

    private void CacheCurrentPositions()
    {
        targetPositions = new Vector3[buttons.Length];

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                continue;
            }

            targetPositions[i] = GetPosition(buttons[i]);
        }
    }

    private void MoveTargetsToDropStart()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                continue;
            }

            SetPosition(buttons[i], targetPositions[i] + startOffset);
        }
    }

    private IEnumerator PlayDropSequence()
    {
        yield return new WaitForSeconds(firstDelay);

        for (int i = 0; i < buttons.Length; i++)
        {
            Transform button = buttons[i];
            if (button == null)
            {
                continue;
            }

            StartCoroutine(DropOne(button, targetPositions[i]));
            yield return new WaitForSeconds(delayBetweenButtons);
        }

        playRoutine = null;
    }

    private IEnumerator DropOne(Transform target, Vector3 endPosition)
    {
        Vector3 startPosition = endPosition + startOffset;
        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            float easedT = dropCurve.Evaluate(t);
            SetPosition(target, Vector3.LerpUnclamped(startPosition, endPosition, easedT));
            yield return null;
        }

        SetPosition(target, endPosition);

        if (addSmallBounce && bounceHeight > 0f && bounceDuration > 0f)
        {
            yield return Bounce(target, endPosition);
        }
    }

    private IEnumerator Bounce(Transform target, Vector3 basePosition)
    {
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float height = Mathf.Sin(t * Mathf.PI) * bounceHeight;
            SetPosition(target, basePosition + Vector3.up * height);
            yield return null;
        }

        SetPosition(target, basePosition);
    }

    private Vector3 GetPosition(Transform target)
    {
        return useLocalPosition ? target.localPosition : target.position;
    }

    private void SetPosition(Transform target, Vector3 position)
    {
        if (useLocalPosition)
        {
            target.localPosition = position;
        }
        else
        {
            target.position = position;
        }
    }
}
