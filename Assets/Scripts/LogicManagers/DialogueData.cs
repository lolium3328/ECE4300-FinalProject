using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueEntry // 把原来的 DialogueLine 改名为 DialogueEntry
{
    public Sprite avatar;
    [TextArea(2, 5)]
    public string content;
}

[CreateAssetMenu(menuName = "Dialogue/DialogueData")]
public class DialogueData : ScriptableObject
{
    // 这里也要相应修改
    public List<DialogueEntry> lines; 
}