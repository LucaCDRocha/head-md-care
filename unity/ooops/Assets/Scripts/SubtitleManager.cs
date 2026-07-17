using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum Language
{
    English,
    French
}

[System.Serializable]
public class SubtitleChunk
{
    [TextArea(2, 3)]
    public string text;
    public float startTime;
    public float duration = 2.0f;
}

[System.Serializable]
public class AutoSubtitleData
{
    public string subtitleName;
    public AudioClip clipToListenFor;
    public List<SubtitleChunk> subtitleChunks = new List<SubtitleChunk>();

    [Header("French Override")]
    public AudioClip frenchClipToListenFor;
    public List<SubtitleChunk> frenchSubtitleChunks = new List<SubtitleChunk>();
}

public class SubtitleManager : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("Drag the TextMeshProUGUI object here (the one inside the background image).")]
    public TextMeshProUGUI subtitleTextUI; 
    [Tooltip("Drag the SubtitleBackground Image object here. This allows us to hide the box when there's no text.")]
    public GameObject subtitleBackgroundObject;

    [Header("What to listen to?")]
    public AudioSource[] audioSourcesToMonitor;

    [Header("Subtitle Database")]
    public List<AutoSubtitleData> subtitles = new List<AutoSubtitleData>();

    [Header("Language Settings")]
    [Tooltip("Select the default starting language.")]
    public Language initialLanguage = Language.English;

    public static Language CurrentLanguage { get; private set; } = Language.English;

    private AudioClip currentlyPlayingClip = null;
    private AudioSource activeAudioSource = null;
    private HashSet<SubtitleChunk> shownChunks = new HashSet<SubtitleChunk>(); 

    private void Awake()
    {
        // Load language preference if saved, default to initialLanguage
        string savedLanguage = PlayerPrefs.GetString("SelectedLanguage", initialLanguage.ToString());
        if (System.Enum.TryParse(savedLanguage, out Language loadedLanguage))
        {
            CurrentLanguage = loadedLanguage;
        }
        else
        {
            CurrentLanguage = initialLanguage;
        }
    }

    private void Start()
    {
        ApplyLanguageSettings();
        ClearSubtitle();
    }

    private void Update()
    {
        // Developer shortcut: Press 'L' to toggle language in Editor or Dev builds
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame)
        {
            Language nextLanguage = CurrentLanguage == Language.English ? Language.French : Language.English;
            ChangeLanguage(nextLanguage);
        }
#endif

        AudioClip clipDetectedThisFrame = null;
        AudioSource sourceDetectedThisFrame = null;

        // 1. Scan for playing audio
        foreach (AudioSource source in audioSourcesToMonitor)
        {
            if (source != null && source.isPlaying)
            {
                clipDetectedThisFrame = source.clip;
                sourceDetectedThisFrame = source;
                break; 
            }
        }

        // 2. Did a NEW audio clip start playing?
        if (clipDetectedThisFrame != currentlyPlayingClip)
        {
            currentlyPlayingClip = clipDetectedThisFrame;
            activeAudioSource = sourceDetectedThisFrame;
            shownChunks.Clear(); 
            ClearSubtitle();
        }

        // 3. If an audio clip is currently playing, check the timings!
        if (currentlyPlayingClip != null && activeAudioSource != null)
        {
            // Search subtitles list using either English or French audio clips
            AutoSubtitleData foundData = subtitles.Find(s => 
                s.clipToListenFor == currentlyPlayingClip || 
                s.frenchClipToListenFor == currentlyPlayingClip);
            
            if (foundData != null)
            {
                float currentAudioTime = activeAudioSource.time;
                bool isAnyTextActiveThisFrame = false;

                // Select which subtitle chunks to display
                List<SubtitleChunk> activeChunks = foundData.subtitleChunks;
                if (CurrentLanguage == Language.French && foundData.frenchSubtitleChunks != null && foundData.frenchSubtitleChunks.Count > 0)
                {
                    activeChunks = foundData.frenchSubtitleChunks;
                }

                foreach (SubtitleChunk chunk in activeChunks)
                {
                    if (currentAudioTime >= chunk.startTime && currentAudioTime < (chunk.startTime + chunk.duration))
                    {
                        ShowSubtitle(chunk.text);
                        isAnyTextActiveThisFrame = true;
                        shownChunks.Add(chunk); 
                        break; 
                    }
                }

                if (!isAnyTextActiveThisFrame)
                {
                     ClearSubtitle();
                }
            }
        }
    }

    public void ChangeLanguage(Language newLanguage)
    {
        CurrentLanguage = newLanguage;
        PlayerPrefs.SetString("SelectedLanguage", newLanguage.ToString());
        PlayerPrefs.Save();

        ApplyLanguageSettings();
        shownChunks.Clear();
        ClearSubtitle();

        Debug.Log("Language switched to: " + newLanguage);
    }

    private void ApplyLanguageSettings()
    {
        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        foreach (AudioSource source in allAudioSources)
        {
            if (source == null) continue;

            // Find if this source's current clip is in our subtitle database
            foreach (var sub in subtitles)
            {
                if (CurrentLanguage == Language.French)
                {
                    // Swap English clip to French clip
                    if (source.clip == sub.clipToListenFor && sub.frenchClipToListenFor != null)
                    {
                        source.clip = sub.frenchClipToListenFor;
                    }
                }
                else
                {
                    // Swap French clip to English clip
                    if (source.clip == sub.frenchClipToListenFor && sub.clipToListenFor != null)
                    {
                        source.clip = sub.clipToListenFor;
                    }
                }
            }
        }
    }

    // Helper functions to manage both the text and the background box
    private void ShowSubtitle(string text)
    {
        if (subtitleTextUI != null) subtitleTextUI.text = text;
        if (subtitleBackgroundObject != null) subtitleBackgroundObject.SetActive(true);
    }

    private void ClearSubtitle()
    {
        if (subtitleTextUI != null) subtitleTextUI.text = "";
        if (subtitleBackgroundObject != null) subtitleBackgroundObject.SetActive(false);
    }
}