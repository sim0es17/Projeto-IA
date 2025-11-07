using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
using ExitGames.Client.Photon;

public class Health : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int maxHealth = 100;   // Vida máxima
    public int health = 100;      // Vida actual
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

    private bool isDead = false; // Flag importante para não levar dano depois de morrer

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<Movement2D>();
    }

    private void Start()
    {
        originalHealthBarsize = healthBar.sizeDelta.x;

        // Garante que a vida inicial não passa do máximo nem fica negativa
        health = Mathf.Clamp(health, 0, maxHealth);

        UpdateHealthUI();
    }

    // ------------------- KNOCKBACK -------------------

    // Método que inicia a Coroutine de Knockback
    public void ApplyKnockback(Vector3 attackerPosition)
    {
        if (rb == null || playerMovement == null) return;

        // Calcula a direção oposta ao atacante
        Vector2 direction = (transform.position - attackerPosition).normalized;

        // Inicia a rotina de knockback
        StartCoroutine(KnockbackRoutine(direction));
    }

    // Coroutine para aplicar a força e controlar o estado do jogador
    private IEnumerator KnockbackRoutine(Vector2 direction)
    {
        // 1. Desativa o controlo do jogador
        playerMovement.SetKnockbackState(true);

        // 2. Aplica a força de impulso
        rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);

        // 3. Espera pela duração do knockback
        yield return new WaitForSeconds(knockbackDuration);

        // 4. Limpa a velocidade horizontal para evitar que o jogador deslize indefinidamente
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // 5. Reativa o controlo do jogador
        playerMovement.SetKnockbackState(false);
    }

    // ------------------- DANO -------------------

    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID = -1)
    {
        if (isDead) return; // Se já estiver morto, ignora dano adicional

        // Retira vida mas nunca abaixo de 0
        health = Mathf.Max(health - _damage, 0);

        UpdateHealthUI();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        // --- Lógica de Knockback (SOMENTE para o Local Player) ---
        if (isLocalPlayer && attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                // Se o atacante tiver um CombatSystem2D, assumimos que é um JOGADOR.
                CombatSystem2D attackerCombat = attackerView.GetComponent<CombatSystem2D>();

                // Se o atacante tiver um EnemyAI, assumimos que é um INIMIGO.
                EnemyAI attackerAI = attackerView.GetComponent<EnemyAI>();

                // Aplicar knockback se for um Jogador OU um Inimigo.
                if (attackerCombat != null || attackerAI != null)
                {
                    // Aplica o knockback usando a posição do atacante
                    ApplyKnockback(attackerView.transform.position);
                }
            }
        }
        // ---------------------------------------------------------

        if (health <= 0)
        {
            isDead = true; // Marca como morto imediatamente
            Debug.Log($"{gameObject.name} morreu!");

            // Atualiza deaths E notifica o atacante (APENAS O CLIENTE LOCAL)
            if (isLocalPlayer) // ou podes usar if (photonView.IsMine)
            {
                // 1. Contar a MORTE (Death)
                int currentDeaths = 0;
                if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Deaths"))
                    currentDeaths = (int)PhotonNetwork.LocalPlayer.CustomProperties["Deaths"];
                currentDeaths++;

                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
                {
                { "Deaths", currentDeaths }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                // 2. Fazer Respawn
                if (RoomManager.instance != null)
                    RoomManager.instance.RespawnPlayer();

                // 3. Notificar o atacante
                if (attackerViewID != -1)
                {
                    PhotonView attackerView = PhotonView.Find(attackerViewID);
                    if (attackerView != null)
                    {
                        // Procura por CombatSystem2D (pois só o Player tem este script com KillConfirmed)
                        CombatSystem2D attackerCombat = attackerView.GetComponent<CombatSystem2D>();
                        if (attackerCombat != null)
                            attackerView.RPC(nameof(CombatSystem2D.KillConfirmed), attackerView.Owner);

                        // NOTA: Se o atacante for um inimigo (EnemyAI), não tem KillConfirmed. 
                        // Apenas jogadores pontuam kills.
                    }
                }
            }

            // Destroi o objeto morto (somente dono)
            PhotonView view = GetComponent<PhotonView>();
            if (view != null && view.IsMine)
                PhotonNetwork.Destroy(gameObject);
        }
    }

    // ------------------- CURA -------------------

    [PunRPC]
    public void Heal(int amount)
    {
        if (isDead) return; // Não cura mortos

        // Soma vida mas sem ultrapassar o máximo
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
