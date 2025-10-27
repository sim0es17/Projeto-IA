using UnityEngine;
using Photon.Pun;
using TMPro;

public class EnemyHealth : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int health = 100;
    public bool IsLocalEnemy;

    [Header("UI")]
    public TextMeshProUGUI healthText;

    void Start()
    {
        if (healthText != null)
            healthText.text = health.ToString();
    }

    // Agora o TakeDamage recebe também o ID de quem causou o dano
    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID = -1)
    {
        health -= _damage;

        if (healthText != null)
            healthText.text = health.ToString();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        // Se a vida chegou a zero
        if (health <= 0)
        {
            Debug.Log($"{gameObject.name} morreu!");

            // Se sou local, respawn
            if (IsLocalEnemy && RoomManagerS.instance != null)
                RoomManagerS.instance.RespawnEnemy();

            // Se há um atacante válido, atribui o kill
            if (attackerViewID != -1)
            {
                PhotonView attackerView = PhotonView.Find(attackerViewID);
                if (attackerView != null)
                {
                    CombatSystem2D attackerCombat = attackerView.GetComponent<CombatSystem2D>();
                    if (attackerCombat != null)
                    {
                        // Dá pontos por kill
                        attackerView.RPC(nameof(CombatSystem2D.RegisterKill), attackerView.Owner);
                    }
                }
            }

            // Destroi o objeto morto (somente o dono o faz)
            PhotonView view = GetComponent<PhotonView>();
            if (view != null && view.IsMine)
                PhotonNetwork.Destroy(gameObject);
        }
    }
}
