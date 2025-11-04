using UnityEngine;

public class CameraFollowLimited : MonoBehaviour
{
    public Transform target;

    [Header("Limites horizontais da câmara")]
    public float minX = -10f;
    public float maxX = 10f;

    [Header("Seguimento vertical")]
    public bool followY = true;
    public float fixedY = 0f;

    // estado interno para bloquear o X
    [HideInInspector] public bool lockX = false;
    [HideInInspector] public float lockedX;

    private void Start()
    {
        if (target == null && transform.parent != null)
            target = transform.parent;

        if (!followY)
            fixedY = transform.position.y;
    }

    public void LockCurrentX()
    {
        lockX = true;
        lockedX = transform.position.x;
    }

    public void UnlockX()
    {
        lockX = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // se estiver bloqueado, usa o X guardado, senão segue o player
        float sourceX = lockX ? lockedX : target.position.x;
        float clampedX = Mathf.Clamp(sourceX, minX, maxX);

        float newY = followY ? target.position.y : fixedY;

        transform.position = new Vector3(clampedX, newY, transform.position.z);
    }
}
