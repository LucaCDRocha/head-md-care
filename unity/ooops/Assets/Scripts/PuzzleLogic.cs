using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class PuzzleLogic : MonoBehaviour, IPointerClickHandler
{
    public enum PuzzlePauseBehavior { HidePieces, ParkOnEdges }

    [Header("Puzzle Setup")]
    [Tooltip("Main body object whose material changes when the puzzle starts.")]
    public GameObject puzzleBodyObject;
    [Tooltip("Material applied to the body object while the puzzle is active.")]
    public Material puzzleBodyMaterial;
    [Tooltip("Empty object that contains all puzzle shards as children.")]
    public Transform puzzlePiecesRoot;
    [Tooltip("How fast the shards drift across the screen.")]
    public float floatingSpeed = 0.45f;
    [Tooltip("How quickly the shards spread away from the center when the puzzle starts.")]
    public float explosionForce = 1.5f;
    [Tooltip("How long the initial explosion speed lasts before the shards settle into floating speed.")]
    public float explosionDuration = 0.8f;
    [Tooltip("Viewport padding used when bouncing shards off the camera borders.")]
    [Range(0.01f, 0.25f)]
    public float borderPadding = 0.08f;

    [Header("Pause & Transition Settings")]
    [Tooltip("Choose how unsnapped pieces behave when the puzzle is paused.")]
    public PuzzlePauseBehavior pauseBehavior = PuzzlePauseBehavior.HidePieces;
    [Tooltip("How fast (in seconds) the pieces transition to the screen edges when parked.")]
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
    
    private bool puzzleChaosActive;
    private float puzzleChaosStartTime;
    private bool hasExploded = false; 
    private Coroutine parkCoroutine;

    private void Start()
    {
        mainCamera = Camera.main;
        CacheBodyRenderers();
        CachePuzzlePieces(); // Will cache and automatically hide pieces at the start
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Only allow explosion to fire once per execution loop
        if (!hasExploded)
        {
            hasExploded = true;
            StartPuzzleChaos();
        }
    }

    private void Update()
    {
        if (!puzzleChaosActive || mainCamera == null || puzzlePieces.Length == 0)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float currentSpeed = Time.time - puzzleChaosStartTime < explosionDuration ? explosionForce : floatingSpeed;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            if (piece == null) continue;

            Draggable draggable = piece.GetComponentInChildren<Draggable>(true);
            if (draggable == null || !draggable.enabled || snappedPieces[i])
            {
                snappedPieces[i] = true;
                pieceVelocities[i] = Vector3.zero;
                continue;
            }

            if (pieceVelocities[i].sqrMagnitude > 0.00001f)
            {
                pieceVelocities[i] = pieceVelocities[i].normalized * currentSpeed;
            }

            piece.position += pieceVelocities[i] * deltaTime;

            Vector3 viewportPoint = mainCamera.WorldToViewportPoint(piece.position);
            Vector3 cameraRight = mainCamera.transform.right;
            Vector3 cameraUp = mainCamera.transform.up;
            float velocityRight = Vector3.Dot(pieceVelocities[i], cameraRight);
            float velocityUp = Vector3.Dot(pieceVelocities[i], cameraUp);
            bool bounced = false;

            if (viewportPoint.x < borderPadding)
            {
                viewportPoint.x = borderPadding;
                velocityRight = Mathf.Abs(velocityRight);
                bounced = true;
            }
            else if (viewportPoint.x > 1f - borderPadding)
            {
                viewportPoint.x = 1f - borderPadding;
                velocityRight = -Mathf.Abs(velocityRight);
                bounced = true;
            }

            if (viewportPoint.y < borderPadding)
            {
                viewportPoint.y = borderPadding;
                velocityUp = Mathf.Abs(velocityUp);
                bounced = true;
            }
            else if (viewportPoint.y > 1f - borderPadding)
            {
                viewportPoint.y = 1f - borderPadding;
                velocityUp = -Mathf.Abs(velocityUp);
                bounced = true;
            }

            pieceVelocities[i] = (cameraRight * velocityRight + cameraUp * velocityUp).normalized * currentSpeed;

            if (bounced)
            {
                viewportPoint.z = pieceDepths[i];
                piece.position = mainCamera.ViewportToWorldPoint(viewportPoint);
            }
        }
    }

    [ContextMenu("Start Puzzle Chaos")]
    public void StartPuzzleChaos()
    {
        if (puzzlePieces.Length == 0) CachePuzzlePieces();
        if (bodyRenderers.Length == 0) CacheBodyRenderers();

        ApplyBodyMaterial();

        if (puzzlePieces.Length == 0) return;

        // Turn off main box collider so clicks hit the floating fragments inside cleanly
        ToggleBodyColliders(false);

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            if (piece == null) continue;

            // Make piece visible on user interaction
            piece.gameObject.SetActive(true);

            Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;
            Vector3 cameraUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;

            Vector3 direction = (cameraRight * UnityEngine.Random.Range(-1f, 1f)) + (cameraUp * UnityEngine.Random.Range(0.35f, 1f));
            if (direction.sqrMagnitude < 0.0001f) direction = cameraRight;

            Vector3 drift = (cameraRight * UnityEngine.Random.Range(-0.2f, 0.2f)) + (cameraUp * UnityEngine.Random.Range(-0.2f, 0.2f));

            pieceVelocities[i] = (direction + drift).normalized * explosionForce;
        }

        puzzleChaosStartTime = Time.time;
        puzzleChaosActive = true;
    }

    [ContextMenu("Pause Puzzle")]
    public void PausePuzzle()
    {
        puzzleChaosActive = false;

        if (parkCoroutine != null)
        {
            StopCoroutine(parkCoroutine);
        }

        if (mainCamera == null) mainCamera = Camera.main;

        // Freeze physical values and turn off fragment click colliders immediately
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

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (puzzlePieces[i] == null || snappedPieces[i]) continue;

            puzzlePieces[i].gameObject.SetActive(true);
            TogglePieceColliders(puzzlePieces[i], true);

            Vector3 cameraRight = mainCamera.transform.right;
            Vector3 cameraUp = mainCamera.transform.up;
            
            Vector3 direction = (cameraRight * UnityEngine.Random.Range(-1f, 1f)) + (cameraUp * UnityEngine.Random.Range(0.35f, 1f));
            Vector3 drift = (cameraRight * UnityEngine.Random.Range(-0.2f, 0.2f)) + (cameraUp * UnityEngine.Random.Range(-0.2f, 0.2f));

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

        ToggleBodyColliders(true);

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
            if (!snappedPieces[i])
            {
                RegisterPieceSnap(i);
                return;
            }
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
            
            ToggleBodyColliders(true); 
            RestoreBodyMaterial();
            ObjectRestored?.Invoke();
        }
    }
}