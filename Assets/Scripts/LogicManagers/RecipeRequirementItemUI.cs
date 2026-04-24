using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class RecipeRequirementItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [FormerlySerializedAs("multiplyText")]
    [SerializeField] private Image multiplyImage;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Icon Sizing")]
    [SerializeField] private Vector2 iconMaxSize = new Vector2(108f, 93f);

    public void Set(Sprite icon, int count)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
            FitIconToMaxSize(icon);
        }

        // Multiplier is now an image asset, so the script only controls visibility.
        if (multiplyImage != null)
        {
            multiplyImage.enabled = true;
        }

        if (countText != null)
        {
            countText.text = count.ToString();
        }
    }

    private void FitIconToMaxSize(Sprite icon)
    {
        if (iconImage == null)
        {
            return;
        }

        RectTransform iconRect = iconImage.rectTransform;
        Vector2 maxSize = iconMaxSize;

        if (maxSize.x <= 0f || maxSize.y <= 0f)
        {
            maxSize = iconRect.sizeDelta;
        }

        if (icon == null)
        {
            iconRect.sizeDelta = maxSize;
            return;
        }

        Vector2 spriteSize = icon.rect.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            iconRect.sizeDelta = maxSize;
            return;
        }

        float scale = Mathf.Min(maxSize.x / spriteSize.x, maxSize.y / spriteSize.y);
        iconRect.sizeDelta = spriteSize * scale;
    }

    private void OnValidate()
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.preserveAspect = true;

        if (iconMaxSize.x <= 0f || iconMaxSize.y <= 0f)
        {
            iconMaxSize = iconImage.rectTransform.sizeDelta;
        }
    }
}
