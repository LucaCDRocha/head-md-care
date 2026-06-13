using UnityEngine;
using UnityEngine.EventSystems; 

public class InteractablePulse : MonoBehaviour, IPointerClickHandler
{
    [Header("Subtle Interaction Glow")]
    public bool enableGlow = true;
    
    [ColorUsage(false, true)] 
    public Color glowColor = new Color(0.1f, 0.1f, 0.1f); 
    public float pulseSpeed = 1.5f;

    [Header("Puzzle Logic Timing")]
    public bool onlyPulseDuringPuzzle = false; 
    public PuzzleLogic puzzleLogic;

    private Renderer[] childRenderers;
    private Material[] instancedMaterials;
    private bool stopPulsing = false;

    private void Start()
    {
        if (onlyPulseDuringPuzzle && puzzleLogic == null) 
        {
            puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
        }

        childRenderers = GetComponentsInChildren<Renderer>();
        instancedMaterials = new Material[childRenderers.Length];

        for (int i = 0; i < childRenderers.Length; i++)
        {
            instancedMaterials[i] = childRenderers[i].material;
            instancedMaterials[i].EnableKeyword("_EMISSION");
        }
    }

    private void Update()
    {
        if (!enableGlow || instancedMaterials == null || instancedMaterials.Length == 0) return;

        bool canPulse = true;
        
        if (onlyPulseDuringPuzzle && puzzleLogic != null)
        {
            canPulse = puzzleLogic.hasExploded && !puzzleLogic.isShattering;
        }

        Color targetEmissionColor = Color.black; 

        if (canPulse && !stopPulsing)
        {
            float intensity = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            intensity = intensity * intensity; 
            targetEmissionColor = glowColor * intensity;
        }

        foreach (Material mat in instancedMaterials)
        {
            if (mat != null) mat.SetColor("_EmissionColor", targetEmissionColor);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 💡 THE FIX 1: If the camera is currently flying through the air, ignore this click entirely!
        if (ObjectFocus.isTransitioning) return;

        // 💡 THE FIX 2: If this object is waiting for the puzzle to explode, ignore clicks until it does!
        if (onlyPulseDuringPuzzle && puzzleLogic != null)
        {
            if (!puzzleLogic.hasExploded || puzzleLogic.isShattering) return;
        }

        StopPulsingPermanently();
    }

    public void StopPulsingPermanently()
    {
        stopPulsing = true;
        
        if (instancedMaterials != null)
        {
            foreach (Material mat in instancedMaterials)
            {
                if (mat != null) mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void OnDestroy()
    {
        if (instancedMaterials != null)
        {
            foreach (Material mat in instancedMaterials)
            {
                if (mat != null) Destroy(mat);
            }
        }
    }
}