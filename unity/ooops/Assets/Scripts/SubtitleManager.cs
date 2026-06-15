using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    private AudioClip currentlyPlayingClip = null;
    private AudioSource activeAudioSource = null;
    private HashSet<SubtitleChunk> shownChunks = new HashSet<SubtitleChunk>(); 

    private void Start()
    {
        ClearSubtitle();
    }

    private void Update()
    {
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
            AutoSubtitleData foundData = subtitles.Find(s => s.clipToListenFor == currentlyPlayingClip);
            
            if (foundData != null)
            {
                float currentAudioTime = activeAudioSource.time;
                bool isAnyTextActiveThisFrame = false;

                foreach (SubtitleChunk chunk in foundData.subtitleChunks)
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