using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class RandomRecipeGenerator
{
    public static bool TryGenerate(JudgeRecipeGenerationConfig config, out RuntimeJudgeRecipe recipe, out string failureReason)
    {
        recipe = null;
        failureReason = string.Empty;

        if (!TryValidateConfig(config, out List<RecipeGenerationRule> usableRules, out int clampedMinTotal, out int clampedMaxTotal, out failureReason))
        {
            return false;
        }

        int attemptCount = Mathf.Max(1, config.MaxGenerationAttempts);
        for (int attempt = 0; attempt < attemptCount; attempt++)
        {
            if (TryGenerateOnce(config, usableRules, clampedMinTotal, clampedMaxTotal, out recipe))
            {
                return true;
            }
        }

        recipe = BuildFallbackRecipe(config, usableRules);
        if (recipe != null)
        {
            failureReason = $"Generator used fallback recipe after {attemptCount} failed attempts.";
            return true;
        }

        failureReason = $"Generator failed after {attemptCount} attempts and no fallback recipe could be built.";
        return false;
    }

    private static bool TryValidateConfig(
        JudgeRecipeGenerationConfig config,
        out List<RecipeGenerationRule> usableRules,
        out int clampedMinTotal,
        out int clampedMaxTotal,
        out string failureReason)
    {
        usableRules = new List<RecipeGenerationRule>();
        clampedMinTotal = 0;
        clampedMaxTotal = 0;
        failureReason = string.Empty;

        if (config == null)
        {
            failureReason = "Generation config is null.";
            return false;
        }

        IReadOnlyList<RecipeGenerationRule> rules = config.Rules;
        for (int i = 0; i < rules.Count; i++)
        {
            RecipeGenerationRule rule = rules[i];
            if (rule == null || rule.maxCount <= 0)
            {
                continue;
            }

            usableRules.Add(rule);
        }

        if (usableRules.Count == 0)
        {
            failureReason = $"Generation config '{config.name}' has no usable rules.";
            return false;
        }

        int requiredBaseCount = 0;
        int maxPossibleCount = 0;
        for (int i = 0; i < usableRules.Count; i++)
        {
            RecipeGenerationRule rule = usableRules[i];
            maxPossibleCount += rule.maxCount;

            if (rule.required)
            {
                requiredBaseCount += GetInitialCount(rule);
            }
        }

        clampedMinTotal = Mathf.Max(config.MinTotalCount, requiredBaseCount);
        clampedMaxTotal = Mathf.Min(config.MaxTotalCount, maxPossibleCount);

        if (clampedMaxTotal < clampedMinTotal)
        {
            failureReason = $"Generation config '{config.name}' is inconsistent. Min total {clampedMinTotal} is greater than max total {clampedMaxTotal}.";
            return false;
        }

        return true;
    }

    private static bool TryGenerateOnce(
        JudgeRecipeGenerationConfig config,
        List<RecipeGenerationRule> usableRules,
        int minTotal,
        int maxTotal,
        out RuntimeJudgeRecipe recipe)
    {
        recipe = null;

        Dictionary<PrefabType, int> counts = new Dictionary<PrefabType, int>();
        int currentTotal = 0;

        for (int i = 0; i < usableRules.Count; i++)
        {
            RecipeGenerationRule rule = usableRules[i];
            if (!rule.required)
            {
                continue;
            }

            int initialCount = GetInitialCount(rule);
            counts[rule.prefabType] = initialCount;
            currentTotal += initialCount;
        }

        int targetMinTotal = Mathf.Max(minTotal, currentTotal);
        if (targetMinTotal > maxTotal)
        {
            return false;
        }

        int targetTotal = UnityEngine.Random.Range(targetMinTotal, maxTotal + 1);
        while (currentTotal < targetTotal)
        {
            List<RecipeGenerationCandidate> candidates = BuildCandidates(usableRules, counts, currentTotal, targetTotal);
            if (candidates.Count == 0)
            {
                return false;
            }

            RecipeGenerationCandidate selected = SelectCandidate(candidates);
            int nextCount = counts.TryGetValue(selected.Rule.prefabType, out int existingCount) ? existingCount + selected.AmountToAdd : selected.AmountToAdd;

            counts[selected.Rule.prefabType] = nextCount;
            currentTotal += selected.AmountToAdd;
        }

        recipe = BuildRecipe(config, counts);
        return recipe != null;
    }

    private static List<RecipeGenerationCandidate> BuildCandidates(
        List<RecipeGenerationRule> usableRules,
        Dictionary<PrefabType, int> counts,
        int currentTotal,
        int targetTotal)
    {
        List<RecipeGenerationCandidate> candidates = new List<RecipeGenerationCandidate>();

        for (int i = 0; i < usableRules.Count; i++)
        {
            RecipeGenerationRule rule = usableRules[i];
            if (IsBlockedByExclusiveCreamSelection(rule.prefabType, counts))
            {
                continue;
            }

            int existingCount = counts.TryGetValue(rule.prefabType, out int count) ? count : 0;

            if (existingCount >= rule.maxCount)
            {
                continue;
            }

            int amountToAdd = existingCount == 0 && !rule.required ? Mathf.Max(1, rule.minCount) : 1;
            amountToAdd = Mathf.Max(1, amountToAdd);

            if (existingCount + amountToAdd > rule.maxCount)
            {
                continue;
            }

            if (currentTotal + amountToAdd > targetTotal)
            {
                continue;
            }

            int weight = Mathf.Max(1, rule.weight);
            candidates.Add(new RecipeGenerationCandidate(rule, amountToAdd, weight));
        }

        return candidates;
    }

    private static RecipeGenerationCandidate SelectCandidate(List<RecipeGenerationCandidate> candidates)
    {
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].Weight;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += candidates[i].Weight;
            if (roll < cumulative)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private static RuntimeJudgeRecipe BuildFallbackRecipe(JudgeRecipeGenerationConfig config, List<RecipeGenerationRule> usableRules)
    {
        Dictionary<PrefabType, int> counts = new Dictionary<PrefabType, int>();

        for (int i = 0; i < usableRules.Count; i++)
        {
            RecipeGenerationRule rule = usableRules[i];
            if (!rule.required)
            {
                continue;
            }

            counts[rule.prefabType] = GetInitialCount(rule);
        }

        if (counts.Count == 0 && usableRules.Count > 0)
        {
            RecipeGenerationRule firstRule = usableRules[0];
            counts[firstRule.prefabType] = GetInitialCount(firstRule);
        }

        return BuildRecipe(config, counts);
    }

    private static RuntimeJudgeRecipe BuildRecipe(JudgeRecipeGenerationConfig config, Dictionary<PrefabType, int> counts)
    {
        if (config == null || counts == null || counts.Count == 0)
        {
            return null;
        }

        List<JudgeRequirementEntry> requirements = new List<JudgeRequirementEntry>();
        foreach (KeyValuePair<PrefabType, int> pair in counts)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            requirements.Add(new JudgeRequirementEntry
            {
                prefabType = pair.Key,
                requiredCount = pair.Value
            });
        }

        if (requirements.Count == 0)
        {
            return null;
        }

        requirements.Sort((left, right) => left.prefabType.CompareTo(right.prefabType));

        string recipeId = Guid.NewGuid().ToString("N");
        string displayName = BuildDisplayName(config.DisplayNamePrefix, requirements);
        return new RuntimeJudgeRecipe(recipeId, displayName, config.RejectUnexpectedTypes, requirements);
    }

    private static bool IsBlockedByExclusiveCreamSelection(PrefabType prefabType, Dictionary<PrefabType, int> counts)
    {
        if (!IsCreamChoice(prefabType))
        {
            return false;
        }

        bool hasCream1 = counts.TryGetValue(PrefabType.Cream1, out int cream1Count) && cream1Count > 0;
        bool hasCream2 = counts.TryGetValue(PrefabType.Cream2, out int cream2Count) && cream2Count > 0;

        return (prefabType == PrefabType.Cream1 && hasCream2) || (prefabType == PrefabType.Cream2 && hasCream1);
    }

    private static bool IsCreamChoice(PrefabType prefabType)
    {
        return prefabType == PrefabType.Cream1 || prefabType == PrefabType.Cream2;
    }

    private static string BuildDisplayName(string prefix, List<JudgeRequirementEntry> requirements)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(string.IsNullOrWhiteSpace(prefix) ? "Random Recipe" : prefix);
        builder.Append(" - ");

        for (int i = 0; i < requirements.Count; i++)
        {
            JudgeRequirementEntry requirement = requirements[i];
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(requirement.prefabType);
            builder.Append(' ');
            builder.Append(requirement.requiredCount);
        }

        return builder.ToString();
    }

    private static int GetInitialCount(RecipeGenerationRule rule)
    {
        int minCount = Mathf.Max(0, rule.minCount);
        if (rule.required)
        {
            minCount = Mathf.Max(1, minCount);
        }

        return Mathf.Clamp(minCount, 1, Mathf.Max(1, rule.maxCount));
    }

    private readonly struct RecipeGenerationCandidate
    {
        public readonly RecipeGenerationRule Rule;
        public readonly int AmountToAdd;
        public readonly int Weight;

        public RecipeGenerationCandidate(RecipeGenerationRule rule, int amountToAdd, int weight)
        {
            Rule = rule;
            AmountToAdd = amountToAdd;
            Weight = weight;
        }
    }
}
