using UnityEngine;

public class CameraFollowLimited : MonoBehaviour
{
    public Transform target;

    [Header("Limites Horizontais")]
    public float minX = -10f;
    public float maxX = 10f;

    [Header("Seguimento Vertical")]
    public bool followY = true;
    public float fixedY = 0f;
    public float minY = -100f; // Limite inferior opcional

    [HideInInspector] public bool lockX = false;
    [HideInInspector] public float lockedX;

    private void Start()
    {
        // Se não tiver alvo, procura o Player pela TAG
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
            else if (transform.parent != null) target = transform.parent;
        }

        if (!followY) fixedY = transform.position.y;
    }

    public void LockCurrentX() { lockX = true; lockedX = transform.position.x; }
    public void UnlockX() { lockX = false; }

    private void LateUpdate()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
            return;
        }

        float sourceX = lockX ? lockedX : target.position.x;
        float clampedX = Mathf.Clamp(sourceX, minX, maxX);

        float targetY = followY ? target.position.y : fixedY;
        float clampedY = Mathf.Clamp(targetY, minY, Mathf.Infinity);

        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }
}