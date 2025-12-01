using UnityEngine;
using System.Collections;

public class SpeedJumpPowerup : MonoBehaviour
{
    [Header("Configuração do Buff")]
    public float duration = 5f; 
    public float speedMultiplier = 1.5f;
    public float jumpMultiplier = 1.3f;

    // REFERÊNCIA CORRIGIDA: Aponta para o novo nome da classe do spawner
    [HideInInspector] 
    public MultiPowerupSpawner spawner; // <-- Tipo Alterado

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Tenta pegar o script de movimento
        Movement2D movement = other.GetComponentInParent<Movement2D>();
        if (movement == null)
            return;

        // 2. Aplica o Buff.
        movement.ActivateSpeedJumpBuff(speedMultiplier, jumpMultiplier, duration);

        // 3. Notifica o Spawner.
        if (spawner != null)
            spawner.PowerupApanhado(); // Chama o método de notificação

        // 4. Destrói o objeto localmente.
        Destroy(gameObject);
    }
}
