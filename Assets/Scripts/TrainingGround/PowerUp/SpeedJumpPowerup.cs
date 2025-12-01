using UnityEngine;
using System.Collections;

public class SpeedJumpPowerup : MonoBehaviour
{
    [Header("Configuração do Buff")]
    public float duration = 5f;       // Duração do efeito em segundos
    public float speedMultiplier = 1.5f;  // Multiplicador de velocidade
    public float jumpMultiplier = 1.3f;   // Multiplicador de força de salto

    // REFERÊNCIA ATUALIZADA: Agora para HealthPowerupSpawner
    [HideInInspector] public HealthPowerupSpawner spawner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Tenta pegar o script de movimento
        // Usamos GetComponentInParent para ser consistente com o HealthPowerup
        Movement2D movement = other.GetComponentInParent<Movement2D>();
        if (movement == null)
            return;

        // 2. Aplica o Buff.
        // Assumimos que o Movement2D tem o método 'ActivateSpeedJumpBuff'.
        movement.ActivateSpeedJumpBuff(speedMultiplier, jumpMultiplier, duration);

        // 3. Notifica o Spawner.
        // O Spawner original (HealthPowerupSpawner) já trata do respawn.
        if (spawner != null)
            spawner.PowerupApanhado();

        // 4. Destrói o objeto localmente.
        Destroy(gameObject);
    }
}
