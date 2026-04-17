using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RecipeGenerationRule
{
    public PrefabType prefabType;
    public bool required;
    [Min(0)] public int minCount = 0;
    [Min(1)] public int maxCount = 1;
    [Min(0)] public int weight = 1;
}

[CreateAssetMenu(fileName = "JudgeRecipeGenerationConfig", menuName = "Game/Judge Recipe Generation Config")]
public class JudgeRecipeGenerationConfig : ScriptableObject
{
    [SerializeField] private string displayNamePrefix = "Random Recipe";
    [SerializeField] [Min(1)] private int minTotalCount = 1;
    [SerializeField] [Min(1)] private int maxTotalCount = 3;
    [SerializeField] private bool rejectUnexpectedTypes = true;
    [SerializeField] [Min(1)] private int maxGenerationAttempts = 32;
    [SerializeField] private List<RecipeGenerationRule> rules = new List<RecipeGenerationRule>();

    public string DisplayNamePrefix => displayNamePrefix;
    public int MinTotalCount => minTotalCount;
    public int MaxTotalCount => maxTotalCount;
    public bool RejectUnexpectedTypes => rejectUnexpectedTypes;
    public int MaxGenerationAttempts => maxGenerationAttempts;
    public IReadOnlyList<RecipeGenerationRule> Rules => rules;
}
