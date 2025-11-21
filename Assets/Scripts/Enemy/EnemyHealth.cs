using UnityEngine;
using Photon.Pun;
using System.Collections;

[RequireComponent(typeof(PhotonView))]
// Removemos o RequireComponent do EnemyAI específico para evitar erros
public class EnemyHealth : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    [Header("Vida")]
    public int maxHealth = 50;
    private int currentHealth;
    private bool isDead = false;

    [Header("UI")]
    public Transform healthBar;
    private float originalHealthBarScaleX;

    private PhotonView photonView;
    
    // MUDANÇA: Agora usamos a classe "Pai"
    private EnemyBase enemyBase; 

    private int mySpawnIndex = -1;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        
        // MUDANÇA: Ele vai encontrar QUALQUER script que herde de EnemyBase (Seja o Simples ou o BFS)
        enemyBase = GetComponent<EnemyBase>(); 

        if (enemyBase == null)
        {
            Debug.LogError("[EnemyHealth] Erro: Nenhum script de IA (EnemyAI ou EnemyAI_BFS) encontrado neste objeto!");
        }

        currentHealth = maxHealth;
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        if (info.photonView.InstantiationData != null && info.photonView.InstantiationData.Length > 0)
        {
            this.mySpawnIndex = (int)info.photonView.InstantiationData[0];
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

    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID = -1)
    {
        if (isDead) return;

        currentHealth -= _damage;
        UpdateHealthBar();

        // --- LÓGICA DE KNOCKBACK E STUN ---
        if (currentHealth > 0 && PhotonNetwork.IsMasterClient && enemyBase != null)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);

            if (attackerView != null)
            {
                Vector2 knockbackDirection = (transform.position - attackerView.transform.position).normalized;

                // MUDANÇA: Usamos o enemyBase para aceder aos valores e chamar o RPC
                // Nota: "ApplyKnockbackRPC" é o nome da função definida no EnemyBase
                photonView.RPC("ApplyKnockbackRPC", RpcTarget.MasterClient,
                                knockbackDirection, enemyBase.KnockbackForce, enemyBase.StunTime);
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

        // MUDANÇA: Desativar o componente base desativa qualquer IA que esteja a ser usada
        if (enemyBase != null) enemyBase.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.isKinematic = true;
        }

        if (attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                attackerView.RPC(nameof(CombatSystem2D.KillConfirmed), attackerView.Owner);
            }
        }

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

    IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (PhotonNetwork.IsMasterClient)
        {
            if (TGRoomManager.instance != null)
            {
                TGRoomManager.instance.RequestEnemyRespawn(mySpawnIndex);
            }
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
