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

    [Header("Screen Boundaries (Brick Walls)")]
    [Range(0.01f, 0.25f)] public float sidePadding = 0.08f;
    [Range(0.01f, 0.5f)] public float bottomPadding = 0.25f;

    [Header("Expulsion Settings")]
    [Tooltip("The transparent helper boundary. Pieces will bounce off this peacefully.")]
    public float expulsionRadius = 3.0f;

    [Header("Pause & Transition Settings")]
    public PuzzlePauseBehavior pauseBehavior = PuzzlePauseBehavior.HidePieces;
    public float parkTransitionDuration = 0.2f;

    public event Action<Transform, int, int> PieceSnapped;
    public event Action ObjectRestored;

    private Camera mainCamera;
    private Camera boundaryCamera; 
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
        if (!puzzleChaosActive || puzzlePieces.Length == 0 || boundaryCamera == null) return;

        float deltaTime = Time.deltaTime;
        bool isGlobalExplosion = Time.time - puzzleChaosStartTime < explosionDuration;

        Vector3 rightAxis = boundaryCamera.transform.right;
        Vector3 upAxis = boundaryCamera.transform.up;

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

            Vector3 puzzleCenter = puzzleBodyObject != null ? puzzleBodyObject.transform.position : puzzlePiecesRoot.position;
            Vector3 toPiece = piece.position - puzzleCenter;
            
            float flatRight = Vector3.Dot(toPiece, rightAxis);
            float flatUp = Vector3.Dot(toPiece, upAxis);
            Vector2 flatOffset = new Vector2(flatRight, flatUp);

            bool isInsideVase = flatOffset.magnitude < expulsionRadius;

            // 💡 BUG FIX 1: Ignore the vase completely during the initial explosion!
            // This lets the shards fly perfectly naturally without forcing them all to the right side!
            if (isGlobalExplosion)
            {
                isInsideVase = false;
            }

            // DETECT DROP
            if (pieceVelocities[i].sqrMagnitude < 0.00001f)
            {
                if (isInsideVase)
                {
                    // 💡 BUG FIX 2: Gentle float out if dropped inside the transparent zone
                    Vector2 eject2D = flatOffset.normalized;
                    if (eject2D.sqrMagnitude < 0.001f) eject2D = new Vector2(UnityEngine.Random.Range(-1f,1f), UnityEngine.Random.Range(-1f,1f)).normalized;
                    Vector3 eject3D = (rightAxis * eject2D.x + upAxis * eject2D.y).normalized;

                    float distanceToEscape = expulsionRadius - flatOffset.magnitude;
                    piece.position += eject3D * distanceToEscape;
                    
                    // No more violent explosion force, just normal drifting!
                    pieceVelocities[i] = eject3D * floatingSpeed; 
                }
                else
                {
                    Vector3 randomDir = (rightAxis * UnityEngine.Random.Range(-1f, 1f) + upAxis * UnityEngine.Random.Range(-1f, 1f)).normalized;
                    pieceVelocities[i] = randomDir * floatingSpeed;
                }
            }
            else if (isInsideVase)
            {
                // 💡 BUG FIX 3: Peaceful Bouncing!
                Vector2 normal2D = flatOffset.normalized;
                if (normal2D.sqrMagnitude < 0.001f) normal2D = Vector2.right;
                Vector3 normal3D = (rightAxis * normal2D.x + upAxis * normal2D.y).normalized;

                // Reflect off the shape exactly like a billiard ball hitting the table edge
                pieceVelocities[i] = Vector3.Reflect(pieceVelocities[i], normal3D).normalized * pieceVelocities[i].magnitude;
                
                float distanceToEscape = expulsionRadius - flatOffset.magnitude;
                piece.position += normal3D * distanceToEscape;
            }

            if (!isGlobalExplosion && pieceVelocities[i].magnitude > floatingSpeed)
            {
                pieceVelocities[i] = Vector3.MoveTowards(pieceVelocities[i], pieceVelocities[i].normalized * floatingSpeed, deltaTime * (explosionForce - floatingSpeed) / 0.5f);
            }

            piece.position += pieceVelocities[i] * deltaTime;

            // Screen Boundaries
            Vector3 viewportPoint = boundaryCamera.WorldToViewportPoint(piece.position);
            bool hitWall = false;

            float velRight = Vector3.Dot(pieceVelocities[i], rightAxis);
            float velUp = Vector3.Dot(pieceVelocities[i], upAxis);

            if (viewportPoint.x <= sidePadding) { viewportPoint.x = sidePadding; hitWall = true; velRight = Mathf.Abs(velRight); }
            else if (viewportPoint.x >= 1f - sidePadding) { viewportPoint.x = 1f - sidePadding; hitWall = true; velRight = -Mathf.Abs(velRight); }

            if (viewportPoint.y <= bottomPadding) { viewportPoint.y = bottomPadding; hitWall = true; velUp = Mathf.Abs(velUp); }
            else if (viewportPoint.y >= 1f - sidePadding) { viewportPoint.y = 1f - sidePadding; hitWall = true; velUp = -Mathf.Abs(velUp); }

            if (hitWall)
            {
                float currentZ = boundaryCamera.WorldToViewportPoint(piece.position).z;
                viewportPoint.z = currentZ;
                piece.position = boundaryCamera.ViewportToWorldPoint(viewportPoint);
                pieceVelocities[i] = (rightAxis * velRight + upAxis * velUp).normalized * pieceVelocities[i].magnitude;
            }
        }
    }

    [ContextMenu("Start Puzzle Chaos")]
    public void StartPuzzleChaos()
    {
        mainCamera = Camera.main;

        if (puzzlePieces.Length == 0) CachePuzzlePieces(); 
        if (bodyRenderers.Length == 0) CacheBodyRenderers(); 

        if (mainCamera != null)
        {
            GameObject dummyObj = GameObject.Find("PuzzleBoundaryAnchor_Internal");
            if (dummyObj == null) dummyObj = new GameObject("PuzzleBoundaryAnchor_Internal");
            
            dummyObj.transform.position = mainCamera.transform.position; 
            dummyObj.transform.rotation = mainCamera.transform.rotation; 

            boundaryCamera = dummyObj.GetComponent<Camera>();
            if (boundaryCamera == null) boundaryCamera = dummyObj.AddComponent<Camera>();
            
            boundaryCamera.CopyFrom(mainCamera); 
            boundaryCamera.enabled = false; 

            for (int i = 0; i < puzzlePieces.Length; i++)
            {
                if (puzzlePieces[i] != null) pieceDepths[i] = boundaryCamera.WorldToViewportPoint(puzzlePieces[i].position).z; 
            }
        }

        ApplyBodyMaterial(); 
        if (puzzlePieces.Length == 0) return; 

        ToggleBodyColliders(false); 

        Vector3 rightAxis = boundaryCamera != null ? boundaryCamera.transform.right : Vector3.right;
        Vector3 upAxis = boundaryCamera != null ? boundaryCamera.transform.up : Vector3.up;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            if (piece == null) continue; 

            piece.gameObject.SetActive(true); 

            float randomX = UnityEngine.Random.Range(-1.2f, 1.2f);
            float randomY = UnityEngine.Random.Range(0.2f, 1.2f);

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
        Vector3 rightAxis = boundaryCamera != null ? boundaryCamera.transform.right : Vector3.right;
        Vector3 upAxis = boundaryCamera != null ? boundaryCamera.transform.up : Vector3.up;

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