using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class PuzzleLogic : MonoBehaviour, IPointerClickHandler
{
    public enum PuzzlePauseBehavior { HidePieces, ParkOnEdges }

    [Header("Puzzle Setup")]
    public GameObject puzzleBodyObject;
    public Material puzzleBodyMaterial;
    public Transform puzzlePiecesRoot;
    public float floatingSpeed = 0.45f;
    public float explosionForce = 1.5f;
    public float explosionDuration = 0.8f;
    [Range(0.01f, 0.25f)]
    public float borderPadding = 0.08f;

    [Header("Expulsion Settings")]
    public float expulsionRadius = 3.0f;

    [Header("Pause & Transition Settings")]
    public PuzzlePauseBehavior pauseBehavior = PuzzlePauseBehavior.HidePieces;
    public float parkTransitionDuration = 0.2f;

    public event Action<Transform, int, int> PieceSnapped;
    public event Action ObjectRestored;

    // Private State Tracking Variables
    private Camera mainCamera;
    private Renderer[] bodyRenderers = new Renderer[0];
    private Material[] originalBodyMaterials = new Material[0];
    private Transform[] puzzlePieces = new Transform[0];
    private Vector3[] originalLocalPositions = new Vector3[0];
    private Quaternion[] originalLocalRotations = new Quaternion[0];
    private Vector3[] pieceVelocities = new Vector3[0];
    private float[] pieceDepths = new float[0];
    private bool[] snappedPieces = new bool[0];
    private Draggable[] pieceDraggables = new Draggable[0];

    private bool puzzleChaosActive;
    private float puzzleChaosStartTime;
    private bool hasExploded = false;
    private Coroutine parkCoroutine;

    private void Start()
    {
        mainCamera = Camera.main;
        CacheBodyRenderers();
        CachePuzzlePieces(); 
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!hasExploded)
        {
            hasExploded = true;
            StartPuzzleChaos();
        }
    }

    private void Update()
    {
        if (!puzzleChaosActive || puzzlePieces.Length == 0 || mainCamera == null) return;

        float deltaTime = Time.deltaTime;
        bool isGlobalExplosion = Time.time - puzzleChaosStartTime < explosionDuration;

        // 💡 THE SCREEN-FLAT FIX: We use the Camera's explicit 2D screen axes!
        Vector3 rightAxis = mainCamera.transform.right;
        Vector3 upAxis = mainCamera.transform.up;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            if (piece == null) continue;

            Draggable draggable = (i < pieceDraggables.Length) ? pieceDraggables[i] : null;

            if (draggable == null || !draggable.enabled || snappedPieces[i])
            {
                snappedPieces[i] = true;
                pieceVelocities[i] = Vector3.zero;
                continue;
            }

            if (draggable.IsBeingDragged)
            {
                pieceVelocities[i] = Vector3.zero; 
                continue;
            }

            // JUST RELEASED DETECTOR:
            if (pieceVelocities[i].sqrMagnitude < 0.00001f)
            {
                Vector3 puzzleCenter = puzzleBodyObject != null ? puzzleBodyObject.transform.position : puzzlePiecesRoot.position;
                Vector3 expulsionDir = piece.position - puzzleCenter;
                
                // Project the 3D distance completely flat against the screen camera view!
                float rightForce = Vector3.Dot(expulsionDir, rightAxis);
                float upForce = Vector3.Dot(expulsionDir, upAxis);
                Vector3 flatDir = (rightAxis * rightForce + upAxis * upForce);
                
                // Now the expulsion radius perfectly matches a visual 2D circle on your iPad screen
                if (flatDir.magnitude < expulsionRadius)
                {
                    if (flatDir.sqrMagnitude < 0.001f) flatDir = upAxis;
                    pieceVelocities[i] = flatDir.normalized * explosionForce;
                }
                else
                {
                    Vector3 randomDir = (rightAxis * UnityEngine.Random.Range(-1f, 1f) + upAxis * UnityEngine.Random.Range(-1f, 1f)).normalized;
                    pieceVelocities[i] = randomDir * floatingSpeed;
                }
            }

            float pieceSpeed = isGlobalExplosion ? explosionForce : floatingSpeed;
            if (!isGlobalExplosion)
            {
                float currentMag = pieceVelocities[i].magnitude;
                if (currentMag > floatingSpeed)
                {
                    pieceSpeed = Mathf.MoveTowards(currentMag, floatingSpeed, deltaTime * (explosionForce - floatingSpeed) / 0.5f);
                }
            }

            if (pieceVelocities[i].sqrMagnitude > 0.00001f)
            {
                pieceVelocities[i] = pieceVelocities[i].normalized * pieceSpeed;
            }

            piece.position += pieceVelocities[i] * deltaTime;

            Vector3 viewportPoint = mainCamera.WorldToViewportPoint(piece.position);
            float velocityRight = Vector3.Dot(pieceVelocities[i], rightAxis);
            float velocityUp = Vector3.Dot(pieceVelocities[i], upAxis);
            bool bounced = false;

            if (viewportPoint.x < borderPadding && velocityRight < 0) { velocityRight = Mathf.Abs(velocityRight); bounced = true; }
            else if (viewportPoint.x > 1f - borderPadding && velocityRight > 0) { velocityRight = -Mathf.Abs(velocityRight); bounced = true; }

            if (viewportPoint.y < borderPadding && velocityUp < 0) { velocityUp = Mathf.Abs(velocityUp); bounced = true; }
            else if (viewportPoint.y > 1f - borderPadding && velocityUp > 0) { velocityUp = -Mathf.Abs(velocityUp); bounced = true; }

            if (bounced)
            {
                pieceVelocities[i] = (rightAxis * velocityRight + upAxis * velocityUp).normalized * pieceSpeed;
            }
        }
    }

    [ContextMenu("Start Puzzle Chaos")]
    public void StartPuzzleChaos()
    {
        mainCamera = Camera.main;

        if (puzzlePieces.Length == 0) CachePuzzlePieces(); 
        if (bodyRenderers.Length == 0) CacheBodyRenderers(); 

        ApplyBodyMaterial(); 
        if (puzzlePieces.Length == 0) return; 

        ToggleBodyColliders(false); 

        Vector3 rightAxis = mainCamera.transform.right;
        Vector3 upAxis = mainCamera.transform.up;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            if (piece == null) continue; 

            piece.gameObject.SetActive(true); 

            float randomX = UnityEngine.Random.Range(-1.2f, 1.2f);
            float randomY = UnityEngine.Random.Range(0.4f, 1.2f);

            Vector3 direction = (rightAxis * randomX) + (upAxis * randomY);
            Vector3 drift = (rightAxis * UnityEngine.Random.Range(-0.15f, 0.15f)) + (upAxis * UnityEngine.Random.Range(-0.15f, 0.15f));

            pieceVelocities[i] = (direction + drift).normalized * explosionForce; 
        }

        puzzleChaosStartTime = Time.time; 
        puzzleChaosActive = true; 
    }

    [ContextMenu("Pause Puzzle")]
    public void PausePuzzle()
    {
        puzzleChaosActive = false;
        if (parkCoroutine != null) StopCoroutine(parkCoroutine);
        if (mainCamera == null) mainCamera = Camera.main;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (puzzlePieces[i] == null || snappedPieces[i]) continue;
            pieceVelocities[i] = Vector3.zero;
            TogglePieceColliders(puzzlePieces[i], false);
        }

        if (pauseBehavior == PuzzlePauseBehavior.HidePieces)
        {
            for (int i = 0; i < puzzlePieces.Length; i++)
            {
                if (puzzlePieces[i] == null || snappedPieces[i]) continue;
                puzzlePieces[i].gameObject.SetActive(false);
            }
        }
        else if (pauseBehavior == PuzzlePauseBehavior.ParkOnEdges)
        {
            parkCoroutine = StartCoroutine(ParkPiecesOverTime());
        }
    }

    [ContextMenu("Resume Puzzle")]
    public void ResumePuzzle()
    {
        if (parkCoroutine != null)
        {
            StopCoroutine(parkCoroutine);
            parkCoroutine = null;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 rightAxis = mainCamera.transform.right;
        Vector3 upAxis = mainCamera.transform.up;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (puzzlePieces[i] == null || snappedPieces[i]) continue;

            puzzlePieces[i].gameObject.SetActive(true);
            TogglePieceColliders(puzzlePieces[i], true);

            Vector3 direction = (rightAxis * UnityEngine.Random.Range(-1f, 1f)) + (upAxis * UnityEngine.Random.Range(0.35f, 1f));
            Vector3 drift = (rightAxis * UnityEngine.Random.Range(-0.2f, 0.2f)) + (upAxis * UnityEngine.Random.Range(-0.2f, 0.2f));

            pieceVelocities[i] = (direction + drift).normalized * explosionForce;
        }

        puzzleChaosStartTime = Time.time;
        puzzleChaosActive = true;
    }

    [ContextMenu("Restore Puzzle Pieces")]
    public void RestorePuzzlePieces()
    {
        puzzleChaosActive = false;
        hasExploded = false;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (puzzlePieces[i] == null) continue;

            puzzlePieces[i].localPosition = originalLocalPositions[i];
            puzzlePieces[i].localRotation = originalLocalRotations[i];
            pieceVelocities[i] = Vector3.zero;
            snappedPieces[i] = false;
        }
        RestoreBodyMaterial();
    }

    public void RegisterPieceSnap()
    {
        for (int i = 0; i < snappedPieces.Length; i++)
        {
            if (!snappedPieces[i]) { RegisterPieceSnap(i); return; }
        }
    }

    public void RegisterPieceSnap(Transform snappedPiece)
    {
        int pieceIndex = GetPieceIndex(snappedPiece);
        if (pieceIndex >= 0) RegisterPieceSnap(pieceIndex);
    }

    public void RegisterPieceSnap(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= puzzlePieces.Length || snappedPieces[pieceIndex]) return;

        snappedPieces[pieceIndex] = true;
        Transform piece = puzzlePieces[pieceIndex];
        if (piece != null)
        {
            piece.localPosition = originalLocalPositions[pieceIndex];
            piece.localRotation = originalLocalRotations[pieceIndex];
            FreezeShard(piece);
        }

        pieceVelocities[pieceIndex] = Vector3.zero;

        int snappedCount = GetSnappedPieceCount();
        int totalPieceCount = puzzlePieces.Length;
        PieceSnapped?.Invoke(piece, snappedCount, totalPieceCount);

        if (snappedCount == puzzlePieces.Length)
        {
            puzzleChaosActive = false;
            hasExploded = false;
            RestoreBodyMaterial();
            ObjectRestored?.Invoke();
        }
    }
}