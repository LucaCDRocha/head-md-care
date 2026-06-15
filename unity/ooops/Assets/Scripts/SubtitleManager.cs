using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 1. We create a new class for individual lines of text
[System.Serializable]
public class SubtitleChunk
{
    [TextArea(2, 3)]
    public string text;
    
    [Tooltip("When should this text appear? (Seconds from the start of the audio)")]
    public float startTime;
    
    [Tooltip("How long should this text stay on screen?")]
    public float duration = 2.0f;
}

// 2. We update our main data class to hold a LIST of chunks
[System.Serializable]
public class AutoSubtitleData
{
    public string subtitleName; // Just to keep your Inspector organized
    public AudioClip clipToListenFor;
    
    [Tooltip("Add the different paragraphs/sentences here, and set their timings!")]
    public List<SubtitleChunk> subtitleChunks = new List<SubtitleChunk>();
}

public class SubtitleManager : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI subtitleUI; 

    [Header("What to listen to?")]
    public AudioSource[] audioSourcesToMonitor;

    [Header("Subtitle Database")]
    public List<AutoSubtitleData> subtitles = new List<AutoSubtitleData>();

    private AudioClip currentlyPlayingClip = null;
    private AudioSource activeAudioSource = null;
    
    // We keep track of which chunks have already been shown
    private HashSet<SubtitleChunk> shownChunks = new HashSet<SubtitleChunk>(); 

    private void Start()
    {
        if (subtitleUI != null) subtitleUI.text = "";
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
            
            // Reset our tracking data for the new clip
            shownChunks.Clear(); 
            if (subtitleUI != null) subtitleUI.text = "";
        }

        // 3. If an audio clip is currently playing, check the timings!
        if (currentlyPlayingClip != null && activeAudioSource != null)
        {
            AutoSubtitleData foundData = subtitles.Find(s => s.clipToListenFor == currentlyPlayingClip);
            
            if (foundData != null)
            {
                // What is the current timestamp of the audio track?
                float currentAudioTime = activeAudioSource.time;

                bool isAnyTextActiveThisFrame = false;

                // Check every chunk in this clip's data
                foreach (SubtitleChunk chunk in foundData.subtitleChunks)
                {
                    // Is the current audio time within this chunk's window? (startTime to startTime + duration)
                    if (currentAudioTime >= chunk.startTime && currentAudioTime < (chunk.startTime + chunk.duration))
                    {
                        if (subtitleUI != null) subtitleUI.text = chunk.text;
                        isAnyTextActiveThisFrame = true;
                        shownChunks.Add(chunk); // Mark it as shown
                        break; // We found the active text, stop checking other chunks
                    }
                }

                // If no text should be showing at this exact second, clear the UI
                if (!isAnyTextActiveThisFrame)
                {
                     if (subtitleUI != null) subtitleUI.text = "";
                }
            }
        }
    }
}