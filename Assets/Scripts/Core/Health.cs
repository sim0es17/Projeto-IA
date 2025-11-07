using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
// Usar o alias completo para Hashtable do Photon para evitar ambiguidade.
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Health : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int maxHealth = 100;   // Vida máxima
    public int health = 100;      // Vida actual
    public bool isLocalPlayer;

    public RectTransform healthBar;
    private float originalHealthBarsize;

    [Header("Knockback")]
    public float knockbackForce;
    public float knockbackDuration;
    private Rigidbody2D rb;
    private Movement2D playerMovement;

    [Header("UI")]
    public TextMeshProUGUI healthText;

    private bool isDead = false;
    private PhotonView view;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<Movement2D>();
        view = GetComponent<PhotonView>();
    }

    private void Start()
    {
        originalHealthBarsize = healthBar.sizeDelta.x;
        health = Mathf.Clamp(health, 0, maxHealth);
        UpdateHealthUI();
    }

    // ------------------- KNOCKBACK -------------------

    public void ApplyKnockback(Vector3 attackerPosition)
    {
        if (rb == null || playerMovement == null || isDead) return;

        Vector2 direction = (transform.position - attackerPosition).normalized;
        StartCoroutine(KnockbackRoutine(direction));
    }

    private IEnumerator KnockbackRoutine(Vector2 direction)
    {
        playerMovement.SetKnockbackState(true);

        rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        playerMovement.SetKnockbackState(false);
    }

    // ------------------- DANO -------------------

    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID = -1)
    {
        if (isDead) return;

        health = Mathf.Max(health - _damage, 0);

        UpdateHealthUI();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        // --- Lógica de Knockback (apenas para o jogador local) ---
        if (view.IsMine && attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                // Assumindo CombatSystem2D ou EnemyAI para identificar o atacante
                if (attackerView.GetComponent<CombatSystem2D>() != null || attackerView.GetComponent<EnemyAI>() != null)
                {
                    ApplyKnockback(attackerView.transform.position);
                }
            }
        }
        // ---------------------------------------------------------

        if (health <= 0)
        {
            isDead = true;
            Debug.Log($"{gameObject.name} morreu!");

            // Lógica SÓ DEVE SER EXECUTADA PELO DONO DO OBJETO MORTO
            if (view.IsMine)
            {
                // 1. Notifica o RoomManager sobre a morte
                if (RoomManager.instance != null)
                {
                    RoomManager.instance.OnPlayerDied(view.Owner);
                }

                // 2. Tenta fazer Respawn
                if (RoomManager.instance != null)
                {
                    RoomManager.instance.RespawnPlayer();
                }

                // 3. Atualiza a contagem de Mortes (Deaths)
                int currentDeaths = 0;
                // Usa o tipo Hashtable correto (já definido no topo)
                if (view.Owner.CustomProperties.ContainsKey("Deaths"))
                    currentDeaths = (int)view.Owner.CustomProperties["Deaths"];
                currentDeaths++;

                Hashtable props = new Hashtable
        {
            { "Deaths", currentDeaths }
        };
                view.Owner.SetCustomProperties(props);

                // 4. Notificar o atacante (para KillConfirmed)
                if (attackerViewID != -1)
                {
                    PhotonView attackerView = PhotonView.Find(attackerViewID);
                    if (attackerView != null)
                    {
                        CombatSystem2D attackerCombat = attackerView.GetComponent<CombatSystem2D>();
                        if (attackerCombat != null)
                            // CORREÇÃO CS0117: RpcTarget.Owner não existe. O correto é RpcTarget.MasterClient se for uma mensagem global
                            // Mas se for só para o Atacante, use: attackerView.Owner (que é o objeto Player)
                            attackerView.RPC(nameof(CombatSystem2D.KillConfirmed), attackerView.Owner);
                    }
                }

                // 5. Destrói o objeto na rede (apenas o dono deve fazê-lo)
                PhotonNetwork.Destroy(gameObject);
            }
        }
    }

    // ------------------- CURA -------------------

    [PunRPC]
    public void Heal(int amount)
    {
        if (isDead) return;

        health = Mathf.Clamp(health + amount, 0, maxHealth);

        UpdateHealthUI();

        Debug.Log($"{gameObject.name} foi curado em {amount}. Vida actual: {health}");
    }

    // ------------------- UI -------------------

    private void UpdateHealthUI()
    {
        if (healthBar != null)
        {
            healthBar.sizeDelta = new Vector2(
              originalHealthBarsize * health / (float)maxHealth,
              healthBar.sizeDelta.y
            );
        }

        if (healthText != null)
        {
            healthText.text = health.ToString();
        }
    }
}