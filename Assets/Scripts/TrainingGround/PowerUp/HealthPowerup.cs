using UnityEngine;
using Photon.Pun;

public class HealthPowerup : MonoBehaviour
{
    public int healAmount = 20;
    
    // REFERÊNCIA CORRIGIDA: Aponta para o novo nome da classe do spawner
    [HideInInspector] 
    public MultiPowerupSpawner spawner; // <-- Tipo Alterado

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health health = other.GetComponentInParent<Health>();
        if (health == null)
            return;

        // 1. Aplica a cura via RPC (lógica existente)
        PhotonView targetView = health.GetComponent<PhotonView>();
        if (targetView != null)
            targetView.RPC("Heal", RpcTarget.All, healAmount);

        // 2. Notifica o Spawner.
        if (spawner != null)
            spawner.PowerupApanhado(); // Chama o método de notificação

        // 3. Destrói o objeto localmente.
        Destroy(gameObject);
    }
}
