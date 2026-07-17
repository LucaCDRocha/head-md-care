using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea(2, 5)]
    [Tooltip("Text displayed when English is active.")]
    public string englishText;

    [TextArea(2, 5)]
    [Tooltip("Text displayed when French is active. If left blank, falls back to English.")]
    public string frenchText;

    private TMP_Text textComponent;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        SubtitleManager.OnLanguageChanged += UpdateText;
        UpdateText();
    }

    private void OnDisable()
    {
        SubtitleManager.OnLanguageChanged -= UpdateText;
    }

    public void UpdateText()
    {
        if (textComponent == null) textComponent = GetComponent<TMP_Text>();
        if (textComponent == null) return;

        bool isFrench = SubtitleManager.CurrentLanguage == Language.French;
        textComponent.text = isFrench && !string.IsNullOrWhiteSpace(frenchText) ? frenchText : englishText;
    }
}
