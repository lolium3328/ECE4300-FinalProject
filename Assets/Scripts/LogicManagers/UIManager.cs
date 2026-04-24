using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Ready State")]
    [SerializeField] private GameObject readyUI;
    [SerializeField] private ReadyRecipeUI readyRecipeUI;

    [FormerlySerializedAs("recipeImage")]
    [SerializeField] private Image legacyRecipeImage; // Transitional reference used only to hide the old static recipe image.

    [SerializeField] private TextMeshProUGUI ready_text;
    [SerializeField] private TextMeshProUGUI cook_text;

    [Header("Finish State")]
    [SerializeField] private GameObject finishUI;
    [SerializeField] private TextMeshProUGUI score_text;

    [Header("Placement Hints")]
    [SerializeField] private GameObject placePancakeUI;
    [SerializeField] private GameObject placeJamUI;
    [SerializeField] private GameObject placeToppingUI;
    [SerializeField] private GameObject chooseToppingHintUI;

    private Coroutine readyCoroutine;
    private Coroutine finishCoroutine;

    private void Start()
    {
        SetActiveIfAssigned(readyUI, false);
        HideLegacyRecipeImage();
        HideReadyRecipeUI();
        SetActiveIfAssigned(ready_text, false);
        SetActiveIfAssigned(cook_text, false);

        SetActiveIfAssigned(finishUI, false);
        SetActiveIfAssigned(score_text, false);

        SetActiveIfAssigned(placePancakeUI, false);
        SetActiveIfAssigned(placeJamUI, false);
        SetActiveIfAssigned(placeToppingUI, false);
        SetActiveIfAssigned(chooseToppingHintUI, false);
    }

    public void TriggerReadyStateUI(RuntimeJudgeRecipe recipe)
    {
        if (readyCoroutine != null)
        {
            StopCoroutine(readyCoroutine);
        }

        SetActiveIfAssigned(readyUI, true);
        HideLegacyRecipeImage();
        HideReadyRecipeUI();
        SetActiveIfAssigned(ready_text, false);
        SetActiveIfAssigned(cook_text, false);

        readyCoroutine = StartCoroutine(ReadyStateUI(recipe));
    }

    private IEnumerator ReadyStateUI(RuntimeJudgeRecipe recipe)
    {
        yield return new WaitForSeconds(0.5f);

        // Render the exact RuntimeJudgeRecipe passed by ProcessManager; do not regenerate here.
        if (readyRecipeUI != null)
        {
            readyRecipeUI.Render(recipe);
            readyRecipeUI.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[UIManager] ReadyRecipeUI is not assigned.", this);
        }

        yield return new WaitForSeconds(5f);
        SetActiveIfAssigned(ready_text, true);

        yield return new WaitForSeconds(2f);
        SetActiveIfAssigned(cook_text, true);

        yield return new WaitForSeconds(0.5f);
        SetActiveIfAssigned(readyUI, false);
        HideReadyRecipeUI();

        readyCoroutine = null;
        if (ProcessManager.Instance != null)
        {
            ProcessManager.Instance.SwitchToNextState();
        }
    }

    public void TriggerFinishStateUI()
    {
        if (finishCoroutine != null)
        {
            StopCoroutine(finishCoroutine);
        }

        SetActiveIfAssigned(finishUI, true);
        SetActiveIfAssigned(score_text, false);

        if (score_text != null && ProcessManager.Instance != null)
        {
            score_text.text = ProcessManager.Instance.Score.ToString();
        }

        finishCoroutine = StartCoroutine(FinishStateUI());
    }

    public void TriggerEndFinishStateUI()
    {
        if (finishCoroutine != null)
        {
            StopCoroutine(finishCoroutine);
            finishCoroutine = null;
        }

        SetActiveIfAssigned(score_text, false);
        SetActiveIfAssigned(finishUI, false);
    }

    private IEnumerator FinishStateUI()
    {
        yield return new WaitForSeconds(1f);
        SetActiveIfAssigned(score_text, true);
        finishCoroutine = null;
    }

    public void TriggerPlacePancakeUI()
    {
        SetActiveIfAssigned(placePancakeUI, true);
    }

    public void TriggerPlaceJamUI()
    {
        SetActiveIfAssigned(placeJamUI, true);
    }

    public void TriggerPlaceToppingUI()
    {
        SetActiveIfAssigned(placeToppingUI, true);
    }

    public void TriggerEndPlacePancakeUI()
    {
        SetActiveIfAssigned(placePancakeUI, false);
    }

    public void TriggerEndPlaceJamUI()
    {
        SetActiveIfAssigned(placeJamUI, false);
    }

    public void TriggerEndPlaceToppingUI()
    {
        SetActiveIfAssigned(placeToppingUI, false);
    }

    public void ChooseToppingHint()
    {
        SetActiveIfAssigned(chooseToppingHintUI, true);
        Debug.Log("choose topping hint triggered");
    }

    public void EndChooseToppingHint()
    {
        SetActiveIfAssigned(chooseToppingHintUI, false);
    }

    private void HideReadyRecipeUI()
    {
        if (readyRecipeUI == null)
        {
            return;
        }

        readyRecipeUI.Clear();
        readyRecipeUI.gameObject.SetActive(false);
    }

    private void HideLegacyRecipeImage()
    {
        if (legacyRecipeImage != null)
        {
            legacyRecipeImage.gameObject.SetActive(false);
        }
    }

    private static void SetActiveIfAssigned(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private static void SetActiveIfAssigned(Component target, bool isActive)
    {
        if (target != null)
        {
            target.gameObject.SetActive(isActive);
        }
    }
}
