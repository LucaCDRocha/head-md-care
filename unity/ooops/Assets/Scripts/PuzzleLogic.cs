using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PuzzleLogic : MonoBehaviour, IPointerClickHandler
{
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
    public event Action<Transform, int, int> PieceSnapped;
    public event Action ObjectRestored;

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

    public enum PuzzlePauseBehavior { HidePieces, ParkOnEdges }

    [Header("Pause Settings")]
    [Tooltip("Choose how unsnapped pieces behave when the puzzle is paused.")]
    public PuzzlePauseBehavior pauseBehavior = PuzzlePauseBehavior.HidePieces;

    [Header("Transition Settings")]
    [Tooltip("How fast (in seconds) the pieces transition to the screen edges when parked.")]
    public float parkTransitionDuration = 0.2f; // Short, snappy default

    private Coroutine parkCoroutine;

    private void Start()
    {
        mainCamera = Camera.main;
        CacheBodyRenderers();
        CachePuzzlePieces();
    }

    [ContextMenu("Cache Body Renderers")]
    public void CacheBodyRenderers()
    {
        if (puzzleBodyObject == null)
        {
            puzzleBodyObject = gameObject;
        }

        bodyRenderers = puzzleBodyObject.GetComponentsInChildren<Renderer>(true);
        originalBodyMaterials = new Material[bodyRenderers.Length];

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            originalBodyMaterials[i] = bodyRenderers[i] != null ? bodyRenderers[i].material : null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Only explode if we haven't already
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
            if (piece == null)
            {
                continue;
            }

            Draggable draggable = piece.GetComponentInChildren<Draggable>(true);
            if (draggable == null || !draggable.enabled)
            {
                snappedPieces[i] = true;
                pieceVelocities[i] = Vector3.zero;
                continue;
            }

            if (snappedPieces[i])
            {
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

    [ContextMenu("Cache Puzzle Pieces")]
    public void CachePuzzlePieces()
    {
        if (puzzlePiecesRoot == null)
        {
            puzzlePieces = new Transform[0];
            originalLocalPositions = new Vector3[0];
            originalLocalRotations = new Quaternion[0];
            pieceVelocities = new Vector3[0];
            pieceDepths = new float[0];
            snappedPieces = new bool[0];
            return;
        }

        List<Transform> pieceList = new List<Transform>();

        Draggable[] draggables = puzzlePiecesRoot.GetComponentsInChildren<Draggable>(true);
        foreach (Draggable draggable in draggables)
        {
            if (draggable != null)
            {
                // Subscribe to the event! 
                // (We unsubscribe first just in case this method gets called twice, preventing double-firing)
                draggable.OnPieceSnapped -= RegisterPieceSnap;
                draggable.OnPieceSnapped += RegisterPieceSnap;

                pieceList.Add(draggable.transform);
            }
        }

        if (pieceList.Count == 0)
        {
            foreach (Transform child in puzzlePiecesRoot)
            {
                pieceList.Add(child);
            }
        }

        puzzlePieces = pieceList.ToArray();
        originalLocalPositions = new Vector3[puzzlePieces.Length];
        originalLocalRotations = new Quaternion[puzzlePieces.Length];
        pieceVelocities = new Vector3[puzzlePieces.Length];
        pieceDepths = new float[puzzlePieces.Length];
        snappedPieces = new bool[puzzlePieces.Length];

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            originalLocalPositions[i] = piece.localPosition;
            originalLocalRotations[i] = piece.localRotation;
            pieceVelocities[i] = Vector3.zero;
            snappedPieces[i] = false;

            if (mainCamera != null)
            {
                pieceDepths[i] = mainCamera.WorldToViewportPoint(piece.position).z;
            }

            piece.gameObject.SetActive(false);
        }
    }

    [ContextMenu("Start Puzzle Chaos")]
    public void StartPuzzleChaos()
    {
        if (puzzlePieces.Length == 0)
        {
            CachePuzzlePieces();
        }

        if (bodyRenderers.Length == 0)
        {
            CacheBodyRenderers();
        }

        ApplyBodyMaterial();

        if (puzzlePieces.Length == 0)
        {
            return;
        }

        Vector3 rootPosition = puzzlePiecesRoot != null ? puzzlePiecesRoot.position : transform.position;

        ToggleBodyColliders(false);

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            if (piece == null)
            {
                continue;
            }

            piece.gameObject.SetActive(true);

            Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;
            Vector3 cameraUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;

            Vector3 direction = (cameraRight * UnityEngine.Random.Range(-1f, 1f)) + (cameraUp * UnityEngine.Random.Range(0.35f, 1f));
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = cameraRight;
            }

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

        ToggleBodyColliders(true);

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (puzzlePieces[i] == null)
            {
                continue;
            }

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
        if (pieceIndex >= 0)
        {
            RegisterPieceSnap(pieceIndex);
        }
    }

    public void RegisterPieceSnap(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= puzzlePieces.Length || snappedPieces[pieceIndex])
        {
            return;
        }

        snappedPieces[pieceIndex] = true;

        Transform piece = puzzlePieces[pieceIndex];
        if (piece != null)
        {
            piece.localPosition = originalLocalPositions[pieceIndex];
            piece.localRotation = originalLocalRotations[pieceIndex];
        }

        pieceVelocities[pieceIndex] = Vector3.zero;

        if (piece != null)
        {
            FreezeShard(piece);
        }

        int snappedCount = GetSnappedPieceCount();
        int totalPieceCount = puzzlePieces.Length;
        PieceSnapped?.Invoke(piece, snappedCount, totalPieceCount);

        if (snappedCount == puzzlePieces.Length)
        {
            puzzleChaosActive = false;

            ToggleBodyColliders(true);

            RestoreBodyMaterial();
            ObjectRestored?.Invoke();
        }
    }

    public int GetSnappedPieceCount()
    {
        int snappedCount = 0;

        for (int i = 0; i < snappedPieces.Length; i++)
        {
            if (snappedPieces[i])
            {
                snappedCount++;
            }
        }

        return snappedCount;
    }

    public Vector3 GetOriginalLocalPosition(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= originalLocalPositions.Length)
        {
            return Vector3.zero;
        }

        return originalLocalPositions[pieceIndex];
    }

    public Quaternion GetOriginalLocalRotation(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= originalLocalRotations.Length)
        {
            return Quaternion.identity;
        }

        return originalLocalRotations[pieceIndex];
    }

    public int GetPieceIndex(Transform piece)
    {
        if (piece == null)
        {
            return -1;
        }

        Transform current = piece;
        while (current != null)
        {
            for (int i = 0; i < puzzlePieces.Length; i++)
            {
                if (puzzlePieces[i] == current)
                {
                    return i;
                }
            }

            current = current.parent;
        }

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (puzzlePieces[i] == piece)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplyBodyMaterial()
    {
        if (puzzleBodyMaterial == null || bodyRenderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
            {
                bodyRenderers[i].material = puzzleBodyMaterial;
            }
        }
    }

    private void RestoreBodyMaterial()
    {
        if (bodyRenderers.Length == 0 || originalBodyMaterials.Length != bodyRenderers.Length)
        {
            return;
        }

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null && originalBodyMaterials[i] != null)
            {
                bodyRenderers[i].material = originalBodyMaterials[i];
            }
        }
    }

    private void FreezeShard(Transform shardRoot)
    {
        Draggable[] draggables = shardRoot.GetComponentsInChildren<Draggable>(true);
        for (int i = 0; i < draggables.Length; i++)
        {
            if (draggables[i] != null)
            {
                draggables[i].enabled = false;
            }
        }

        Collider[] colliders = shardRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        Rigidbody[] rigidbodies = shardRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null)
            {
                rigidbodies[i].linearVelocity = Vector3.zero;
                rigidbodies[i].angularVelocity = Vector3.zero;
                rigidbodies[i].isKinematic = true;
            }
        }
    }

    [ContextMenu("Pause Puzzle")]
    public void PausePuzzle()
    {
        puzzleChaosActive = false;

        // Cancel any active parking animation if Pause is spammed
        if (parkCoroutine != null)
        {
            StopCoroutine(parkCoroutine);
        }

        if (mainCamera == null) mainCamera = Camera.main;

        // Instantly halt velocities and remove hitboxes so background elements can be clicked
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
            // Start the smooth movement routine!
            parkCoroutine = StartCoroutine(ParkPiecesOverTime());
        }
    }

    [ContextMenu("Resume Puzzle")]
    public void ResumePuzzle()
    {
        // Stop the parking animation if it's still running
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

            // Give them a fresh physical kick to scatter away from the edges
            Vector3 cameraRight = mainCamera.transform.right;
            Vector3 cameraUp = mainCamera.transform.up;

            Vector3 direction = (cameraRight * UnityEngine.Random.Range(-1f, 1f)) + (cameraUp * UnityEngine.Random.Range(0.35f, 1f));
            Vector3 drift = (cameraRight * UnityEngine.Random.Range(-0.2f, 0.2f)) + (cameraUp * UnityEngine.Random.Range(-0.2f, 0.2f));

            pieceVelocities[i] = (direction + drift).normalized * explosionForce;
        }

        puzzleChaosStartTime = Time.time;
        puzzleChaosActive = true;
    }

    // Helper method to cleanly turn colliders on/off during pauses
    private void TogglePieceColliders(Transform pieceRoot, bool enable)
    {
        Collider[] colliders = pieceRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enable;
        }
    }

    private System.Collections.IEnumerator ParkPiecesOverTime()
    {
        // 1. Lock down the exact piece count right now so mid-frame array changes can't break our loops
        int pieceCount = puzzlePieces.Length;

        Vector3[] startPositions = new Vector3[pieceCount];
        Vector3[] targetPositions = new Vector3[pieceCount];

        // Gather all current positions and precalculate their precise destination targets
        for (int i = 0; i < pieceCount; i++)
        {
            // Safety check: ensure index is within current bounds of all tracking arrays
            if (puzzlePieces[i] == null || i >= snappedPieces.Length || snappedPieces[i]) continue;

            startPositions[i] = puzzlePieces[i].position;

            Vector3 viewportPos = mainCamera.WorldToViewportPoint(startPositions[i]);
            float distToLeft = Mathf.Abs(viewportPos.x - 0f);
            float distToRight = Mathf.Abs(viewportPos.x - 1f);
            float distToBottom = Mathf.Abs(viewportPos.y - 0f);
            float distToTop = Mathf.Abs(viewportPos.y - 1f);

            float minDistance = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);

            if (minDistance == distToLeft) viewportPos.x = 0f;
            else if (minDistance == distToRight) viewportPos.x = 1f;
            else if (minDistance == distToBottom) viewportPos.y = 0f;
            else viewportPos.y = 1f;

            if (i < pieceDepths.Length)
            {
                viewportPos.z = pieceDepths[i];
            }

            targetPositions[i] = mainCamera.ViewportToWorldPoint(viewportPos);
        }

        // 2. Linearly interpolate positions over the duration using our locked pieceCount
        float elapsed = 0f;
        while (elapsed < parkTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / parkTransitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, normalizedTime);

            for (int i = 0; i < pieceCount; i++)
            {
                if (puzzlePieces[i] == null || i >= snappedPieces.Length || snappedPieces[i]) continue;
                puzzlePieces[i].position = Vector3.Lerp(startPositions[i], targetPositions[i], smoothT);
            }

            yield return null; // Wait for the next frame
        }

        // 3. Absolute final pass using our locked pieceCount to ensure precise math alignment
        for (int i = 0; i < pieceCount; i++)
        {
            if (puzzlePieces[i] == null || i >= snappedPieces.Length || snappedPieces[i]) continue;
            puzzlePieces[i].position = targetPositions[i];
        }

        parkCoroutine = null;
    }

    private void ToggleBodyColliders(bool enable)
    {
        // Use the assigned puzzle body object, or fallback to this gameObject
        GameObject target = puzzleBodyObject != null ? puzzleBodyObject : gameObject;

        // Get the colliders directly on the main body shell
        Collider[] colliders = target.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = enable;
            }
        }
    }
}
