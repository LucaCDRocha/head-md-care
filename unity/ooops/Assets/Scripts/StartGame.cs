using UnityEngine;
using UnityEngine.EventSystems; // Required for cross-platform click/touch events
using Unity.Cinemachine;       // Required for Cinemachine v3

public class StartGame : MonoBehaviour, IPointerClickHandler
{
    [Header("Cinemachine Cameras")]
    [Tooltip("The camera looking at the main menu or starting shot.")]
    public CinemachineCamera menuCamera;

    [Tooltip("The game camera looking at the puzzle vase that we want to switch to instantly.")]
    public CinemachineCamera puzzleCamera;

    [Header("Optional Settings")]
    [Tooltip("If true, the script will automatically hide this starting object asset after it is clicked.")]
    public bool hideOnStart = true;

    // Works beautifully on both PC Mouse clicks and Tablet Touch screens!
    public void OnPointerClick(PointerEventData eventData)
    {
        // Fallback: If you didn't assign the puzzle camera, look for it by its default name
        if (puzzleCamera == null)
        {
            puzzleCamera = GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
        }

        if (puzzleCamera == null)
        {
            Debug.LogError($"StartGame on {gameObject.name} cannot find your primary Puzzle Camera!");
            return;
        }

        Debug.Log("Start Game triggered! Shifting camera perspective.");

        // 1. Swap priorities so Cinemachine targets the puzzle view
        puzzleCamera.Priority = 30;

        if (menuCamera != null)
        {
            menuCamera.Priority = 10;
        }

        // 2. Clean up this object so players can't click it again
        if (hideOnStart)
        {
            gameObject.SetActive(false); // Hides the button/object entirely
        }
        else
        {
            enabled = false; // Just turns off the click script component
        }
    }
}