using UnityEngine;
using UnityEngine.EventSystems; // 💡 NEW: Needed to detect clicks!

// 💡 THE FIX: Added IPointerClickHandler right to the pulse script
public class InteractablePulse : MonoBehaviour, IPointerClickHandler
{
    [Header("Subtle Interaction Glow")]
    public bool enableGlow = true;
    
    [ColorUsage(false, true)] 
    public Color glowColor = new Color(0.1f, 0.1f, 0.1f); 
    public float pulseSpeed = 1.5f;

    [Header("Puzzle Logic Timing")]
    [Tooltip("Check this box for puzzle shards (they wait for the explosion). UNCHECK this for the Coffee Mug (glows immediately)!")]
    public bool onlyPulseDuringPuzzle = false; // 💡 NEW: Let's you override the timing!
    
    [Tooltip("Only needed if the box above is checked.")]
    public PuzzleLogic puzzleLogic;

    private Renderer[] childRenderers;
    private Material[] instancedMaterials;
    private bool stopPulsing = false;

    private void Start()
    {
        // Only bother looking for the puzzle logic if this specific object needs it
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
        
        // If this is a puzzle piece, check to make sure the game is actually playable right now
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

    // 💡 NEW: The script now listens for clicks all by itself!
    public void OnPointerClick(PointerEventData eventData)
    {
        StopPulsingPermanently();
    }

    public void StopPulsingPermanently()
    {
        stopPulsing = true;
        
        // Force the color to black immediately so it doesn't freeze mid-glow
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