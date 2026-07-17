using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class LocalizedTexture : MonoBehaviour
{
    [Tooltip("Texture displayed when English is active.")]
    public Texture englishTexture;

    [Tooltip("Texture displayed when French is active. If left blank, falls back to English.")]
    public Texture frenchTexture;

    [Tooltip("The shader property name for the texture. Default is '_MainTex'.")]
    public string texturePropertyName = "_MainTex";

    [Tooltip("If the renderer has multiple materials, specify the index to swap. Default is 0.")]
    public int materialIndex = 0;

    private Renderer meshRenderer;

    private void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        SubtitleManager.OnLanguageChanged += UpdateTexture;
        UpdateTexture();
    }

    private void OnDisable()
    {
        SubtitleManager.OnLanguageChanged -= UpdateTexture;
    }

    public void UpdateTexture()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        if (meshRenderer == null) return;

        bool isFrench = SubtitleManager.CurrentLanguage == Language.French;
        Texture activeTexture = isFrench && frenchTexture != null ? frenchTexture : englishTexture;

        if (activeTexture != null)
        {
            Material[] mats = meshRenderer.materials;
            if (mats != null && materialIndex < mats.Length && mats[materialIndex] != null)
            {
                mats[materialIndex].SetTexture(texturePropertyName, activeTexture);
            }
        }
    }
}
