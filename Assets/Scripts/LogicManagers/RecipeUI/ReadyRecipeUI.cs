using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RecipeVisualEntry
{
    public PrefabType prefabType;
    public Sprite icon;
}

public class ReadyRecipeUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform recipePanelRoot;
    [SerializeField] private RecipeRequirementItemUI recipeItemPrefab;

    [Header("Visual Mapping")]
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private List<RecipeVisualEntry> visuals = new List<RecipeVisualEntry>();

    [Header("Options")]
    [SerializeField] private bool clearChildrenOnRender = true;
    [SerializeField] private bool logMissingVisuals = true;

    public void Render(RuntimeJudgeRecipe recipe)
    {
        Clear();

        if (recipe == null)
        {
            Debug.LogWarning("[ReadyRecipeUI] No runtime recipe was provided.", this);
            return;
        }

        if (recipeItemPrefab == null)
        {
            Debug.LogWarning("[ReadyRecipeUI] Recipe item prefab is not assigned.", this);
            return;
        }

        Transform root = GetRecipePanelRoot();
        if (root == null)
        {
            Debug.LogWarning("[ReadyRecipeUI] Recipe panel root is not assigned.", this);
            return;
        }

        foreach (JudgeRequirementEntry requirement in recipe.Requirements)
        {
            if (requirement == null || requirement.requiredCount <= 0)
            {
                continue;
            }

            RecipeVisualEntry visual = FindVisual(requirement.prefabType);
            Sprite icon = visual != null && visual.icon != null ? visual.icon : fallbackIcon;

            RecipeRequirementItemUI item = Instantiate(recipeItemPrefab, root);
            item.gameObject.SetActive(true);
            item.Set(icon, requirement.requiredCount);
        }
    }

    public void Clear()
    {
        if (!clearChildrenOnRender)
        {
            return;
        }

        Transform root = GetRecipePanelRoot();
        if (root == null)
        {
            return;
        }

        // Rebuild the panel from the current recipe every time it is shown.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (recipeItemPrefab != null && child == recipeItemPrefab.gameObject)
            {
                child.SetActive(false);
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private Transform GetRecipePanelRoot()
    {
        return recipePanelRoot != null ? recipePanelRoot : transform;
    }

    private RecipeVisualEntry FindVisual(PrefabType prefabType)
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            RecipeVisualEntry visual = visuals[i];
            if (visual != null && visual.prefabType == prefabType)
            {
                return visual;
            }
        }

        if (logMissingVisuals)
        {
            Debug.LogWarning($"[ReadyRecipeUI] Missing visual mapping for PrefabType '{prefabType}'.", this);
        }

        return null;
    }
}
