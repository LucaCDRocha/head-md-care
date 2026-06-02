using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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
    [Tooltip("Viewport padding used when bouncing shards off the camera borders.")]
    [Range(0.01f, 0.25f)]
    public float borderPadding = 0.08f;
    [Tooltip("Stage manager that updates the room when a shard is restored.")]
    public StageManager stageManager;

    public UnityEvent OnPieceSnapped;
    public UnityEvent OnObjectRestored;

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
        StartPuzzleChaos();
    }

    private void Update()
    {
        if (!puzzleChaosActive || mainCamera == null || puzzlePieces.Length == 0)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (snappedPieces[i])
            {
                continue;
            }

            Transform piece = puzzlePieces[i];
            if (piece == null)
            {
                continue;
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

            pieceVelocities[i] = cameraRight * velocityRight + cameraUp * velocityUp;

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
        foreach (Transform child in puzzlePiecesRoot)
        {
            pieceList.Add(child);
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

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            Transform piece = puzzlePieces[i];
            if (piece == null)
            {
                continue;
            }

            Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;
            Vector3 cameraUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;

            Vector3 direction = (cameraRight * Random.Range(-1f, 1f)) + (cameraUp * Random.Range(0.35f, 1f));
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = cameraRight;
            }

            Vector3 drift = (cameraRight * Random.Range(-0.2f, 0.2f)) + (cameraUp * Random.Range(-0.2f, 0.2f));

            pieceVelocities[i] = (direction + drift).normalized * floatingSpeed;
            pieceVelocities[i] += direction.normalized * explosionForce;
        }

        puzzleChaosActive = true;
    }

    [ContextMenu("Restore Puzzle Pieces")]
    public void RestorePuzzlePieces()
    {
        puzzleChaosActive = false;

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

        OnPieceSnapped?.Invoke();

        if (stageManager != null)
        {
            stageManager.AdvanceStoryStep(GetSnappedPieceCount() - 1);
        }

        if (GetSnappedPieceCount() == puzzlePieces.Length)
        {
            puzzleChaosActive = false;
            RestoreBodyMaterial();
            OnObjectRestored?.Invoke();
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
}
