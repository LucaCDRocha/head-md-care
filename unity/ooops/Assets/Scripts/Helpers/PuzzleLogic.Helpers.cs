using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PuzzleLogic // Linked automatically to the primary file
{
    [ContextMenu("Cache Body Renderers")]
    public void CacheBodyRenderers()
    {
        if (puzzleBodyObject == null) puzzleBodyObject = gameObject;

        bodyRenderers = puzzleBodyObject.GetComponentsInChildren<Renderer>(true);
        originalBodyMaterials = new Material[bodyRenderers.Length];

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            originalBodyMaterials[i] = bodyRenderers[i] != null ? bodyRenderers[i].material : null;
        }
    }

    [ContextMenu("Cache Puzzle Pieces")]
    public void CachePuzzlePieces()
    {
        if (puzzlePiecesRoot == null)
        {
            puzzlePieces = new Transform[0];
            pieceDraggables = new Draggable[0]; // Reset optimization array
            originalLocalPositions = new Vector3[0];
            originalLocalRotations = new Quaternion[0];
            pieceVelocities = new Vector3[0];
            pieceDepths = new float[0];
            snappedPieces = new bool[0];
            return;
        }

        List<Transform> pieceList = new List<Transform>();
        List<Draggable> draggableList = new List<Draggable>();
        Draggable[] draggables = puzzlePiecesRoot.GetComponentsInChildren<Draggable>(true);
        
        foreach (Draggable draggable in draggables)
        {
            if (draggable != null)
            {
                draggable.OnPieceSnapped -= RegisterPieceSnap;
                draggable.OnPieceSnapped += RegisterPieceSnap;
                pieceList.Add(draggable.transform);
                draggableList.Add(draggable); // Store component reference safely
            }
        }

        if (pieceList.Count == 0)
        {
            foreach (Transform child in puzzlePiecesRoot)
            {
                pieceList.Add(child);
                draggableList.Add(child.GetComponentInChildren<Draggable>(true));
            }
        }

        puzzlePieces = pieceList.ToArray();
        pieceDraggables = draggableList.ToArray(); // Save optimization array to memory
        
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

    private IEnumerator ParkPiecesOverTime()
    {
        int pieceCount = puzzlePieces.Length;
        Vector3[] startPositions = new Vector3[pieceCount];
        Vector3[] targetPositions = new Vector3[pieceCount];

        for (int i = 0; i < pieceCount; i++)
        {
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

            if (i < pieceDepths.Length) viewportPos.z = pieceDepths[i];
            
            targetPositions[i] = mainCamera.ViewportToWorldPoint(viewportPos);
        }

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
            
            yield return null;
        }

        for (int i = 0; i < pieceCount; i++)
        {
            if (puzzlePieces[i] == null || i >= snappedPieces.Length || snappedPieces[i]) continue;
            puzzlePieces[i].position = targetPositions[i];
        }

        parkCoroutine = null;
    }

    private void ToggleBodyColliders(bool enable)
    {
        GameObject target = puzzleBodyObject != null ? puzzleBodyObject : gameObject;
        Collider[] colliders = target.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enable;
        }
    }

    private void TogglePieceColliders(Transform pieceRoot, bool enable)
    {
        Collider[] colliders = pieceRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enable;
        }
    }

    private void ApplyBodyMaterial()
    {
        if (puzzleBodyMaterial == null || bodyRenderers.Length == 0) return;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null) bodyRenderers[i].material = puzzleBodyMaterial;
        }
    }

    private void RestoreBodyMaterial()
    {
        if (bodyRenderers.Length == 0 || originalBodyMaterials.Length != bodyRenderers.Length) return;
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
            if (draggables[i] != null) draggables[i].enabled = false;
        }

        TogglePieceColliders(shardRoot, false);

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

    public int GetSnappedPieceCount()
    {
        int snappedCount = 0;
        for (int i = 0; i < snappedPieces.Length; i++)
        {
            if (snappedPieces[i]) snappedCount++;
        }
        return snappedCount;
    }

    public Vector3 GetOriginalLocalPosition(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= originalLocalPositions.Length) return Vector3.zero;
        return originalLocalPositions[pieceIndex];
    }

    public Quaternion GetOriginalLocalRotation(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= originalLocalRotations.Length) return Quaternion.identity;
        return originalLocalRotations[pieceIndex];
    }

    public int GetPieceIndex(Transform piece)
    {
        if (piece == null) return -1;

        Transform current = piece;
        while (current != null)
        {
            for (int i = 0; i < puzzlePieces.Length; i++)
            {
                if (puzzlePieces[i] == current) return i;
            }
            current = current.parent;
        }

        for (int i = 0; i < puzzlePieces.Length; i++)
        {
            if (puzzlePieces[i] == piece) return i;
        }

        return -1;
    }
}