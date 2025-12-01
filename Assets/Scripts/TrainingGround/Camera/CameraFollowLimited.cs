using UnityEngine;
using Photon.Pun;

public class CameraFollowLimited : MonoBehaviour
{
    public Transform target;

    [Header("Limites")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -100f; // Limite inferior

    [Header("Seguimento")]
    public bool followY = true;
    public float fixedY = 0f;

    // --- VARIÁVEIS RESTAURADAS PARA O ZOOM FUNCIONAR ---
    [HideInInspector] public bool lockX = false;
    [HideInInspector] public float lockedX;

    private void Start()
    {
        // 1. Verifica se esta câmara pertence ao MEU jogador
        // Procura o PhotonView no objeto pai (o Soldier/Orc)
        PhotonView pv = GetComponentInParent<PhotonView>();

        if (pv != null && !pv.IsMine)
        {
            // Se esta câmara é do INIMIGO, desliga-a!
            gameObject.SetActive(false);
            return;
        }

        // 2. Se for a MINHA câmara, define o alvo como o meu Pai
        if (target == null && transform.parent != null)
        {
            target = transform.parent;
        }

        if (!followY) fixedY = transform.position.y;

        // Garante que é a câmara principal
        GetComponent<Camera>().tag = "MainCamera";
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // --- LÓGICA DE BLOQUEIO (RESTORED) ---
        // Se o Zoom mandou bloquear (lockX), usamos a posição guardada.
        // Se não, usamos a posição do jogador.
        float sourceX = lockX ? lockedX : target.position.x;

        float clampedX = Mathf.Clamp(sourceX, minX, maxX);

        float targetY = followY ? target.position.y : fixedY;
        float clampedY = Mathf.Clamp(targetY, minY, Mathf.Infinity);

        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }

    // --- MÉTODOS QUE FALTAVAM (PARA O ERRO DESAPARECER) ---
    public void LockCurrentX()
    {
        lockX = true;
        lockedX = transform.position.x;
    }

    public void UnlockX()
    {
        lockX = false;
    }
}