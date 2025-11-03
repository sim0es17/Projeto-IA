using UnityEngine;

public class CameraFollowLimited : MonoBehaviour
{
    public Transform target;          // normalmente o jogador

    [Header("Limites horizontais da câmara")]
    public float minX = -10f;
    public float maxX = 10f;

    [Header("Seguimento vertical")]
    public bool followY = true;       // se queres que a câmara siga o Y do player
    public float fixedY = 0f;         // se não seguir, usa este Y fixo

    private void Start()
    {
        // Se não definiste um alvo, tenta usar o parent (Soldier/Player)
        if (target == null && transform.parent != null)
            target = transform.parent;

        // Se não for para seguir em Y, guarda o Y inicial da câmara
        if (!followY)
            fixedY = transform.position.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float clampedX = Mathf.Clamp(target.position.x, minX, maxX);
        float newY = followY ? target.position.y : fixedY;

        transform.position = new Vector3(clampedX, newY, transform.position.z);
    }
}
