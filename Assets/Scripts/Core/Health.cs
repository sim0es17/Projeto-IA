using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;

public class Health : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int maxHealth = 100;    // Vida máxima
    public int health = 100;       // Vida actual
    public bool isLocalPlayer;

    public RectTransform healthBar;
    private float originalHealthBarsize;

    [Header("Knockback")]
    // Estes valores só serão usados se o atacante não fornecer a força (Fallback).
    public float knockbackForceFallback = 10f;
    public float knockbackDurationFallback = 0.3f;

    private Rigidbody2D rb;
    private Movement2D playerMovement;
    private bool isKnockedBack = false; // Flag para evitar knockback sobreposto

    [Header("UI")]
    public TextMeshProUGUI healthText;

    private bool isDead = false;
    private PhotonView view;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // É CRÍTICO que o componente Movement2D seja encontrado para gerir o estado de controlo.
        playerMovement = GetComponent<Movement2D>();
        view = GetComponent<PhotonView>();
    }

    private void Start()
    {
        if (healthBar != null)
        {
            // Garante que a barra de vida existe antes de tentar obter o tamanho
            originalHealthBarsize = healthBar.sizeDelta.x;
        }

        health = Mathf.Clamp(health, 0, maxHealth);
        UpdateHealthUI();
    }

    // ---------------------------------------------------------------------------------
    // --- 1. LÓGICA DE DANO E SINCRONIZAÇÃO DE REDE ---
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// RPC UNIFICADO para receber dano. Esta assinatura (int, int, float, float)
    /// corrige o erro de compatibilidade com o EnemyAI.
    /// Os parâmetros float são opcionais (default 0f) para chamadas simples.
    /// </summary>
    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID, float attackerKnockbackForce = 0f, float attackerKnockbackDuration = 0f)
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
                // Usa a força/duração do atacante se for > 0, senão usa o fallback
                float finalForce = (attackerKnockbackForce > 0) ? attackerKnockbackForce : knockbackForceFallback;
                float finalDuration = (attackerKnockbackDuration > 0) ? attackerKnockbackDuration : knockbackDurationFallback;

                ApplyKnockback(
                    attackerView.transform.position,
                    finalForce,
                    finalDuration
                );
            }
        }
        // ---------------------------------------------------------

        if (health <= 0)
        {
            isDead = true;
            HandleDeath(attackerViewID);
        }
    }

    /// <summary>
    /// Processa toda a lógica que deve ocorrer quando o jogador morre.
    /// </summary>
    private void HandleDeath(int attackerViewID)
    {
        Debug.Log($"{gameObject.name} morreu!");

        // Lógica SÓ DEVE SER EXECUTADA PELO DONO DO OBJETO MORTO
        if (!view.IsMine) return;

        // 1. Atualiza a contagem de Mortes (Deaths) no Photon
        int currentDeaths = 0;
        if (view.Owner.CustomProperties.ContainsKey("Deaths"))
            currentDeaths = (int)view.Owner.CustomProperties["Deaths"];
        currentDeaths++;

        Hashtable props = new Hashtable
        {
            { "Deaths", currentDeaths }
        };
        view.Owner.SetCustomProperties(props);

        // 2. Notificar o atacante (para KillConfirmed)
        if (attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                // Assumindo que CombatSystem2D está no atacante.
                // NOTE: Não se deve chamar RPCs diretamente para um componente,
                // mas sim para o View, especificando o método.
                attackerView.RPC("KillConfirmed", attackerView.Owner);
            }
        }

        // 3. Destrói o objeto na rede
        PhotonNetwork.Destroy(gameObject);

        // NOTA: Se o seu jogo tiver um sistema de respawn (RoomManager),
        // deve ser chamado aqui após a destruição.
    }


    // ---------------------------------------------------------------------------------
    // --- 2. LÓGICA DE KNOCKBACK ---
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Aplica a repulsão ao jogador.
    /// </summary>
    public void ApplyKnockback(Vector3 attackerPosition, float force, float duration)
    {
        // Se já estiver em knockback, morto, ou se não tiver os componentes necessários, ignora.
        if (rb == null || playerMovement == null || isDead || isKnockedBack) return;

        // 1. Calcula a direção horizontal (e adiciona um pequeno Y para levantar)
        Vector2 direction = (transform.position - attackerPosition).normalized;
        if (direction.y < 0.2f) direction.y = 0.2f;
        direction = direction.normalized;

        // 2. Inicia a corrotina com os valores do atacante
        StartCoroutine(KnockbackRoutine(direction, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 direction, float force, float duration)
    {
        isKnockedBack = true;
        // playerMovement.SetKnockbackState(true) - Assume que este método bloqueia o input.
        if (playerMovement != null) playerMovement.SetKnockbackState(true);

        rb.linearVelocity = Vector2.zero; // Zera a velocidade
        rb.AddForce(direction * force, ForceMode2D.Impulse); // Aplica o Impulso

        yield return new WaitForSeconds(duration);

        // Termina o knockback.
        if (playerMovement != null) playerMovement.SetKnockbackState(false);
        isKnockedBack = false;
    }


    // ---------------------------------------------------------------------------------
    // --- 3. LÓGICA DE CURA E UI ---
    // ---------------------------------------------------------------------------------

    [PunRPC]
    public void Heal(int amount)
    {
        if (isDead) return;

        health = Mathf.Clamp(health + amount, 0, maxHealth);

        UpdateHealthUI();

        Debug.Log($"{gameObject.name} foi curado em {amount}. Vida actual: {health}");
    }

    private void UpdateHealthUI()
    {
        // Atualiza a barra de vida
        if (healthBar != null && originalHealthBarsize > 0)
        {
            healthBar.sizeDelta = new Vector2(
                originalHealthBarsize * health / (float)maxHealth,
                healthBar.sizeDelta.y
            );
        }

        // Atualiza o texto de vida
        if (healthText != null)
        {
            healthText.text = health.ToString();
        }
    }
}