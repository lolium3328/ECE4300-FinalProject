using UnityEngine;

public class RecipeRoundController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TriggerBoxJudge targetJudge;
    [SerializeField] private JudgeRecipeGenerationConfig generationConfig;

    [Header("Behavior")]
    [SerializeField] private bool generateOnStart;
    [SerializeField] private bool logGeneratedRecipe = true;

    [Header("Debug Output")]
    [SerializeField] [TextArea(6, 16)] private string lastGenerationSummary;

    private RuntimeJudgeRecipe currentRecipe;
/// <summary>
/// A
/// </summary>
    public RuntimeJudgeRecipe CurrentRecipe => currentRecipe != null ? currentRecipe.Clone() : null;

    private void Reset()
    {
        if (targetJudge == null)
        {
            targetJudge = GetComponent<TriggerBoxJudge>();
        }
    }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateAndApplyRecipe();
        }
    }

    [ContextMenu("Generate And Apply Recipe")]
    public void GenerateAndApplyRecipe()
    {
        if (targetJudge == null)
        {
            lastGenerationSummary = "[RecipeRoundController] Missing TriggerBoxJudge reference.";
            if (logGeneratedRecipe)
            {
                Debug.LogWarning(lastGenerationSummary, this);
            }
            return;
        }

        if (!RandomRecipeGenerator.TryGenerate(generationConfig, out RuntimeJudgeRecipe generatedRecipe, out string generationMessage))
        {
            lastGenerationSummary = $"[RecipeRoundController] Failed to generate recipe. {generationMessage}";
            if (logGeneratedRecipe)
            {
                Debug.LogWarning(lastGenerationSummary, this);
            }
            return;
        }

        currentRecipe = generatedRecipe;
        targetJudge.SetRuntimeRecipe(generatedRecipe);

        lastGenerationSummary = BuildControllerSummary(generatedRecipe, generationMessage);
        if (logGeneratedRecipe)
        {
            Debug.Log(lastGenerationSummary, this);
        }
    }

    [ContextMenu("Generate Apply And Judge")]
    public void GenerateApplyAndJudge()
    {
        GenerateAndApplyRecipe();

        if (currentRecipe != null && targetJudge != null)
        {
            targetJudge.JudgeNow();
        }
    }

    [ContextMenu("Clear Runtime Recipe")]
    public void ClearRuntimeRecipe()
    {
        currentRecipe = null;
        lastGenerationSummary = "[RecipeRoundController] Cleared runtime recipe.";

        if (targetJudge != null)
        {
            targetJudge.ClearRuntimeRecipe();
        }

        if (logGeneratedRecipe)
        {
            Debug.Log(lastGenerationSummary, this);
        }
    }

    private string BuildControllerSummary(RuntimeJudgeRecipe generatedRecipe, string generationMessage)
    {
        string summary = "[RecipeRoundController] Generated runtime recipe.\n";

        if (!string.IsNullOrWhiteSpace(generationMessage))
        {
            summary += $"{generationMessage}\n";
        }

        summary += generatedRecipe != null ? generatedRecipe.BuildSummary() : "Recipe: none";
        return summary;
    }
}
