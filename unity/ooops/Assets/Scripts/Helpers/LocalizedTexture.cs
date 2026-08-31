using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps materials or textures on a Renderer or UI component based on active language setting in SubtitleManager.
/// Supports both material swapping (recommended) and texture swapping.
/// </summary>
public class LocalizedTexture : MonoBehaviour
{
    [Header("Language Materials (Recommended)")]
    [Tooltip("Material displayed when English is active. Overrides texture setting if provided.")]
    public Material englishMaterial;

    [Tooltip("Material displayed when French is active. Overrides texture setting if provided.")]
    public Material frenchMaterial;

    [Header("Language Textures (Fallback)")]
    [Tooltip("Texture displayed when English is active.")]
    public Texture englishTexture;

    [Tooltip("Texture displayed when French is active. If left blank, falls back to English.")]
    public Texture frenchTexture;

    [Header("Texture Shader Settings")]
    [Tooltip("The shader property name for texture swapping. Leave blank to auto-detect (_BaseMap for URP, _MainTex for Built-In).")]
    public string texturePropertyName = "";

    [Tooltip("If the renderer has multiple materials, specify the index to swap. Default is 0.")]
    public int materialIndex = 0;

    private Renderer meshRenderer;
    private RawImage rawImage;
    private Image uiImage;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        if (rawImage == null) rawImage = GetComponent<RawImage>();
        if (uiImage == null) uiImage = GetComponent<Image>();
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
        CacheComponents();

        bool isFrench = SubtitleManager.CurrentLanguage == Language.French;

        // 1. Material Swap Priority
        Material activeMaterial = (isFrench && frenchMaterial != null) ? frenchMaterial : englishMaterial;
        if (activeMaterial != null)
        {
            if (meshRenderer != null)
            {
                if (Application.isPlaying)
                {
                    Material[] mats = meshRenderer.materials;
                    if (mats != null && materialIndex >= 0 && materialIndex < mats.Length)
                    {
                        mats[materialIndex] = activeMaterial;
                        meshRenderer.materials = mats;
                    }
                }
                else
                {
                    Material[] sharedMats = meshRenderer.sharedMaterials;
                    if (sharedMats != null && materialIndex >= 0 && materialIndex < sharedMats.Length)
                    {
                        sharedMats[materialIndex] = activeMaterial;
                        meshRenderer.sharedMaterials = sharedMats;
                    }
                }

                NotifyPulse();
            }
            else if (uiImage != null)
            {
                uiImage.material = activeMaterial;
            }
            else if (rawImage != null)
            {
                rawImage.material = activeMaterial;
            }
            return;
        }

        // 2. Texture Swap Fallback
        Texture activeTexture = (isFrench && frenchTexture != null) ? frenchTexture : englishTexture;
        if (activeTexture != null)
        {
            if (meshRenderer != null)
            {
                if (Application.isPlaying)
                {
                    Material[] mats = meshRenderer.materials;
                    if (mats != null && materialIndex >= 0 && materialIndex < mats.Length && mats[materialIndex] != null)
                    {
                        Material mat = mats[materialIndex];
                        string propName = string.IsNullOrEmpty(texturePropertyName)
                            ? (mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex")
                            : texturePropertyName;

                        mat.SetTexture(propName, activeTexture);
                        meshRenderer.materials = mats;
                    }
                }
                else
                {
                    Material[] sharedMats = meshRenderer.sharedMaterials;
                    if (sharedMats != null && materialIndex >= 0 && materialIndex < sharedMats.Length && sharedMats[materialIndex] != null)
                    {
                        Material sharedMat = sharedMats[materialIndex];
                        string propName = string.IsNullOrEmpty(texturePropertyName)
                            ? (sharedMat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex")
                            : texturePropertyName;

                        sharedMat.SetTexture(propName, activeTexture);
                        meshRenderer.sharedMaterials = sharedMats;
                    }
                }

                NotifyPulse();
            }
            else if (rawImage != null)
            {
                rawImage.texture = activeTexture;
            }
            else if (uiImage != null && activeTexture is Texture2D tex2D)
            {
                uiImage.sprite = Sprite.Create(tex2D, new Rect(0, 0, tex2D.width, tex2D.height), new Vector2(0.5f, 0.5f));
            }
        }
    }

    private void NotifyPulse()
    {
        InteractablePulse pulse = GetComponent<InteractablePulse>();
        if (pulse == null) pulse = GetComponentInParent<InteractablePulse>();
        if (pulse == null) pulse = GetComponentInChildren<InteractablePulse>();

        if (pulse != null)
        {
            pulse.RefreshMaterials();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            UpdateTexture();
        }
    }
}
