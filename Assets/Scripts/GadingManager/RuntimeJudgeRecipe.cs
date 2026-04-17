using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class JudgeRequirementEntry
{
    public PrefabType prefabType;
    [Min(0)] public int requiredCount = 1;
}
//用于运行时候克隆原本的scriptable object的类，防止运行时修改了原本的scriptable object数据
[Serializable]
public class RuntimeJudgeRecipe
{
    [SerializeField] private string recipeId;
    [SerializeField] private string displayName;
    [SerializeField] private bool rejectUnexpectedTypes = true;
    [SerializeField] private List<JudgeRequirementEntry> requirements = new List<JudgeRequirementEntry>();

    public string RecipeId => recipeId;
    public string DisplayName => displayName;
    public bool RejectUnexpectedTypes => rejectUnexpectedTypes;
    public IReadOnlyList<JudgeRequirementEntry> Requirements => requirements;

    public RuntimeJudgeRecipe(string recipeId, string displayName, bool rejectUnexpectedTypes, List<JudgeRequirementEntry> requirements)
    {
        this.recipeId = recipeId;
        this.displayName = displayName;
        this.rejectUnexpectedTypes = rejectUnexpectedTypes;
        this.requirements = CloneRequirements(requirements);
    }

    public RuntimeJudgeRecipe Clone()
    {
        return new RuntimeJudgeRecipe(recipeId, displayName, rejectUnexpectedTypes, requirements);
    }

    public string BuildSummary()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Recipe ID: {recipeId}");
        builder.AppendLine($"Display Name: {displayName}");
        builder.AppendLine($"Reject Unexpected Types: {rejectUnexpectedTypes}");
        builder.AppendLine("Requirements:");

        if (requirements.Count == 0)
        {
            builder.AppendLine("- none");
            return builder.ToString();
        }

        for (int i = 0; i < requirements.Count; i++)
        {
            JudgeRequirementEntry requirement = requirements[i];
            if (requirement == null)
            {
                continue;
            }

            builder.AppendLine($"- {requirement.prefabType}: {requirement.requiredCount}");
        }

        return builder.ToString();
    }

    private static List<JudgeRequirementEntry> CloneRequirements(List<JudgeRequirementEntry> source)
    {
        List<JudgeRequirementEntry> clone = new List<JudgeRequirementEntry>();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Count; i++)
        {
            JudgeRequirementEntry entry = source[i];
            if (entry == null)
            {
                continue;
            }

            clone.Add(new JudgeRequirementEntry
            {
                prefabType = entry.prefabType,
                requiredCount = entry.requiredCount
            });
        }

        return clone;
    }
}
