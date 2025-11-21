using UnityEngine;
using Photon.Pun;

public class SpeedJumpPowerup : MonoBehaviour
{
    [Header("Configuração do Buff")]
    public float duration = 5f;        // Duração do efeito em segundos
    public float speedMultiplier = 1.5f; // 50% mais rápido
    public float jumpMultiplier = 1.3f;  // 30% mais alto

    [HideInInspector] public SpeedJumpPowerupSpawner spawner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Tenta pegar o script de movimento
        Movement2D movement = other.GetComponentInParent<Movement2D>();
        
        if (movement == null)
            return;

        // Verifica se quem apanhou é o dono do jogador (importante para o Photon)
        PhotonView targetView = movement.GetComponent<PhotonView>();

        // Só ativamos se o jogador for o dono (IsMine), 
        // pois o movimento é processado localmente
        if (targetView != null && targetView.IsMine)
        {
            movement.ActivateSpeedJumpBuff(duration, speedMultiplier, jumpMultiplier);
        }

        // Avisa o spawner que foi apanhado (para iniciar o cooldown de respawn)
        if (spawner != null)
            spawner.PowerupApanhado();

        // Destroi o objeto visualmente
        Destroy(gameObject);
    }
}
