using UnityEngine;
using Photon.Pun;
using System.Collections;
using TMPro; // Certifica-te que tens o TextMeshPro importado se o usares

[RequireComponent(typeof(PhotonView), typeof(EnemyAI))]
public class EnemyHealth : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int maxHealth = 50;
    private int currentHealth;
    private bool isDead = false;

    [Header("UI (Opcional)")]
    // Transform que tem a escala X alterada para representar a barra de vida
    public Transform healthBar;
    private float originalHealthBarScaleX;

    private PhotonView photonView;
    private EnemyAI enemyAI;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        enemyAI = GetComponent<EnemyAI>(); // Obtém a referência ao script de IA
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (healthBar != null)
        {
            originalHealthBarScaleX = healthBar.localScale.x;
            UpdateHealthBar();
        }
    }

    /// <summary>
    /// Recebe dano. É chamado via RPC pelo jogador atacante (CombatSystem2D).
    /// </summary>
    /// <param name="_damage">Quantidade de dano.</param>
    /// <param name="attackerViewID">PhotonViewID do jogador atacante.</param>
    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID = -1)
    {
        if (isDead) return;

        currentHealth -= _damage;
        UpdateHealthBar();

        // DEBUG PARA MOSTRAR A VIDA
        Debug.Log($"[EnemyHealth] {gameObject.name} (Inimigo) recebeu {_damage} de dano. Vida restante: {currentHealth}/{maxHealth}");

        // --- LÓGICA DE KNOCKBACK E STUN ---
        // Se ainda estiver vivo e se o Master Client estiver a executar (para controle de física)
        if (currentHealth > 0 && PhotonNetwork.IsMasterClient && enemyAI != null)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);

            if (attackerView != null)
            {
                // Calcula a direção de repulsão (oposta ao atacante)
                Vector2 knockbackDirection = (transform.position - attackerView.transform.position).normalized;

                // O próprio EnemyHealth aciona o RPC de Knockback no Master Client (que é o servidor da IA)
                photonView.RPC(nameof(EnemyAI.ApplyKnockbackRPC), RpcTarget.MasterClient,
                                knockbackDirection, enemyAI.KnockbackForce, enemyAI.StunTime);
            }
        }
        // ------------------------------------

        if (currentHealth <= 0)
        {
            Die(attackerViewID);
        }
    }

    void Die(int attackerViewID)
    {
        isDead = true;

        // Desativar a IA e o movimento imediatamente
        if (enemyAI != null) enemyAI.enabled = false;

        // Desativar física (Rb)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.isKinematic = true;
        }

        // Notificar o atacante (Kills/Score)
        if (attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                // Chama KillConfirmed APENAS no cliente do atacante (dono do view)
                // O KillConfirmed está no CombatSystem2D do jogador.
                attackerView.RPC(nameof(CombatSystem2D.KillConfirmed), attackerView.Owner);
            }
        }

        // Destruição do objeto de rede (apenas o Master Client)
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DestroyAfterDelay(1.0f));
        }
    }

    void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            float healthRatio = (float)currentHealth / maxHealth;
            Vector3 newScale = healthBar.localScale;
            newScale.x = originalHealthBarScaleX * healthRatio;
            healthBar.localScale = newScale;
        }
    }

    /// <summary>
    /// Aguarda um tempo (para animação) e destrói o objeto, pedindo o respawn.
    /// </summary>
    IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (PhotonNetwork.IsMasterClient)
        {
            // 1. ANTES de destruir, pede ao Room Manager para agendar um respawn.
            if (TGRoomManager.instance != null)
            {
                TGRoomManager.instance.RequestEnemyRespawn(transform.position);
            }

            // 2. Destrói o objeto de rede
            PhotonNetwork.Destroy(gameObject);
        }
    }
}