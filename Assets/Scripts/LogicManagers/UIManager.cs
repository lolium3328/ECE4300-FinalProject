using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject readyUI;
    [SerializeField] private Image recipeImage;
    [SerializeField] private TextMeshProUGUI ready_text;
    [SerializeField] private TextMeshProUGUI cook_text;

    [SerializeField] private GameObject finishUI;
    [SerializeField] private TextMeshProUGUI score_text;
    [SerializeField] private GameObject placePancakeUI;
    [SerializeField] private GameObject placeJamUI;
    [SerializeField] private GameObject placeToppingUI;
    [SerializeField] private GameObject chooseToppingHintUI;

    private void Start()
    {
        readyUI.SetActive(false);
        recipeImage.gameObject.SetActive(false);
        ready_text.gameObject.SetActive(false);
        cook_text.gameObject.SetActive(false);

        finishUI.SetActive(false);
        score_text.gameObject.SetActive(false);

        placePancakeUI.SetActive(false);
        placeJamUI.SetActive(false);
        placeToppingUI.SetActive(false);
        chooseToppingHintUI.SetActive(false);
    }

    private void Update()
    {
        
    }

    public void TriggerReadyStateUI()
    {
        readyUI.SetActive(true);
        recipeImage.gameObject.SetActive(false);
        ready_text.gameObject.SetActive(false);
        cook_text.gameObject.SetActive(false);

        StartCoroutine(ReadyStateUI());
    }

    private IEnumerator ReadyStateUI()
    {
        yield return new WaitForSeconds(0.5f);
        recipeImage.gameObject.SetActive(true);
        yield return new WaitForSeconds(5f);
        ready_text.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        cook_text.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        readyUI.SetActive(false);
        ProcessManager.Instance.SwitchToNextState();   //动画放完后切换状态
    }

    public void TriggerFinishStateUI()
    {
        finishUI.SetActive(true);
        score_text.gameObject.SetActive(false);
        score_text.text = ProcessManager.Instance.Score.ToString();
        StartCoroutine(FinishStateUI());
    }

    public void TriggerEndFinishStateUI()
    {
        if (FinishStateUI() != null)
        {
            StopCoroutine(FinishStateUI());
        }
        score_text.gameObject.SetActive(false);
        finishUI.SetActive(false);
    }

    private IEnumerator FinishStateUI()
    {
        yield return new WaitForSeconds(1f);
        score_text.gameObject.SetActive(true);
    }

    public void TriggerPlacePancakeUI()
    {
        placePancakeUI.SetActive(true);
    }

    public void TriggerPlaceJamUI()
    {
        placeJamUI.SetActive(true);
    }

    public void TriggerPlaceToppingUI()
    {
        placeToppingUI.SetActive(true);
    }

    public void TriggerEndPlacePancakeUI()
    {
        placePancakeUI.SetActive(false);
    }

    public void TriggerEndPlaceJamUI()
    {
        placeJamUI.SetActive(false);
    }

    public void TriggerEndPlaceToppingUI()
    {
        placeToppingUI.SetActive(false);
    }

    public void ChooseToppingHint()
    {
        chooseToppingHintUI.SetActive(true);
        Debug.Log("choose topping hint triggered");
    }

    public void EndChooseToppingHint()
    {
        chooseToppingHintUI.SetActive(false);
    }
}