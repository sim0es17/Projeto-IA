using UnityEngine;
using Photon.Pun;
using System.Collections;
using TMPro; // Necessário se estiveres a usar TextMeshPro

// Adiciona a interface IPunInstantiateMagicCallback
[RequireComponent(typeof(PhotonView), typeof(EnemyAI))]
public class EnemyHealth : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
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

    // Variável para guardar o índice do seu spawn point
    private int mySpawnIndex = -1;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        enemyAI = GetComponent<EnemyAI>(); // Obtém a referência ao script de IA
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Chamado pela Photon quando este objeto é instanciado pela rede.
    /// Usamos isto para receber o spawnIndex enviado pelo TGRoomManager.
    /// </summary>
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // info.photonView.InstantiationData contém os dados enviados pelo Instantiate
        if (info.photonView.InstantiationData != null && info.photonView.InstantiationData.Length > 0)
        {
            this.mySpawnIndex = (int)info.photonView.InstantiationData[0];
            Debug.Log($"[EnemyHealth] Inimigo instanciado. O meu Spawn Point é o {mySpawnIndex}");
        }
        else
        {
            Debug.LogWarning($"[EnemyHealth] Inimigo instanciado sem um Spawn Index! O respawn limitado pode não funcionar.");
        }
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

        // Debug de Vida
        Debug.Log($"[EnemyHealth] {gameObject.name} (Inimigo) recebeu {_damage} de dano. Vida restante: {currentHealth}/{maxHealth} (SpawnIndex: {mySpawnIndex})");

        // --- LÓGICA DE KNOCKBACK E STUN ---
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
                // ENVIA O ÍNDICE (mySpawnIndex), não a posição
                TGRoomManager.instance.RequestEnemyRespawn(mySpawnIndex);
            }

            // 2. Destrói o objeto de rede
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
