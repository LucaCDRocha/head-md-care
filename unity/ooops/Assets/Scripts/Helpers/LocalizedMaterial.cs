using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps materials on a Renderer or UI Graphic based on the active language setting in SubtitleManager.
/// </summary>
[DisallowMultipleComponent]
public class LocalizedMaterial : MonoBehaviour
{
    [Header("Language Materials")]
    [Tooltip("Material displayed when English is active.")]
    public Material englishMaterial;

    [Tooltip("Material displayed when French is active. If left blank, falls back to English.")]
    public Material frenchMaterial;

    [Header("Target Options")]
    [Tooltip("If the Renderer has multiple materials, specify the index to swap. Default is 0.")]
    public int materialIndex = 0;

    private Renderer targetRenderer;
    private Graphic targetGraphic;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        SubtitleManager.OnLanguageChanged += UpdateMaterial;
        UpdateMaterial();
    }

    private void OnDisable()
    {
        SubtitleManager.OnLanguageChanged -= UpdateMaterial;
    }

    public void UpdateMaterial()
    {
        CacheComponents();

        bool isFrench = SubtitleManager.CurrentLanguage == Language.French;
        Material activeMaterial = (isFrench && frenchMaterial != null) ? frenchMaterial : englishMaterial;

        if (activeMaterial == null) return;

        if (targetRenderer != null)
        {
            if (Application.isPlaying)
            {
                Material[] mats = targetRenderer.materials;
                if (mats != null && materialIndex >= 0 && materialIndex < mats.Length)
                {
                    mats[materialIndex] = activeMaterial;
                    targetRenderer.materials = mats;
                }
            }
            else
            {
                Material[] sharedMats = targetRenderer.sharedMaterials;
                if (sharedMats != null && materialIndex >= 0 && materialIndex < sharedMats.Length)
                {
                    sharedMats[materialIndex] = activeMaterial;
                    targetRenderer.sharedMaterials = sharedMats;
                }
            }
        }
        else if (targetGraphic != null)
        {
            targetGraphic.material = activeMaterial;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            UpdateMaterial();
        }
    }
}
