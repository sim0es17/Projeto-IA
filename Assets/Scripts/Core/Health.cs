using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime; // Necessário para usar Player/RoomManager

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
        playerMovement = GetComponent<Movement2D>();
        view = GetComponent<PhotonView>();
    }

    private void Start()
    {
        // Garante que a barra de vida existe antes de tentar obter o tamanho
        if (healthBar != null)
        {
            originalHealthBarsize = healthBar.sizeDelta.x;
        }

        health = Mathf.Clamp(health, 0, maxHealth);
        UpdateHealthUI();
    }

    // --- 1. LÓGICA DE KNOCKBACK ---

    /// <summary>
    /// Aplica a repulsão ao jogador.
    /// </summary>
    public void ApplyKnockback(Vector3 attackerPosition, float force, float duration)
    {
        // Se já estiver em knockback ou morto, ignora.
        if (rb == null || playerMovement == null || isDead || isKnockedBack) return;

        // 1. Calcula a direção
        Vector2 direction = (transform.position - attackerPosition).normalized;

        // 2. Garante um Y mínimo para descolar do chão
        if (direction.y < 0.2f) direction.y = 0.2f;
        direction = direction.normalized;

        // 3. Inicia a corrotina com os valores do atacante
        StartCoroutine(KnockbackRoutine(direction, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 direction, float force, float duration)
    {
        isKnockedBack = true;
        playerMovement.SetKnockbackState(true); // Bloqueia o controlo no Movement2D

        // Zera a velocidade atual para garantir que o impulso é aplicado corretamente
        rb.linearVelocity = Vector2.zero;

        // Aplica a força de IMPULSO
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(duration);

        // Termina o knockback.
        playerMovement.SetKnockbackState(false);
        isKnockedBack = false;
    }

    // --- 2. LÓGICA DE DANO E SINCRONIZAÇÃO DE REDE ---

    /// <summary>
    /// RPC de dano simples (chamado pelo CombatSystem2D).
    /// Assinatura compatível: (int, int)
    /// </summary>
    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID)
    {
        // Chama a lógica de dano completa, usando os valores de fallback (0f, 0f)
        // que serão tratados pelo TakeDamageFull.
        TakeDamageFull(_damage, attackerViewID, 0f, 0f);
    }

    /// <summary>
    /// Recebe dano com todos os parâmetros de knockback.
    /// </summary>
    [PunRPC]
    public void TakeDamageFull(int _damage, int attackerViewID, float attackerKnockbackForce, float attackerKnockbackDuration)
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
                // Se force/duration vierem a 0 (do TakeDamage simples), usa-se o fallback.
                float finalForce = (attackerKnockbackForce > 0) ? attackerKnockbackForce : knockbackForceFallback;
                float finalDuration = (attackerKnockbackDuration > 0) ? attackerKnockbackDuration : knockbackDurationFallback;

                // Aplica o knockback
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
            Debug.Log($"{gameObject.name} morreu!");

            // Lógica SÓ DEVE SER EXECUTADA PELO DONO DO OBJETO MORTO
            if (view.IsMine)
            {
                // 1. Notifica o RoomManager sobre a morte
                // NOTA: Assumindo que tem um RoomManager.instance
                // if (RoomManager.instance != null) { RoomManager.instance.OnPlayerDied(view.Owner); }

                // 2. Tenta fazer Respawn
                // if (RoomManager.instance != null) { RoomManager.instance.RespawnPlayer(); }

                // 3. Atualiza a contagem de Mortes (Deaths)
                int currentDeaths = 0;
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
                            // Envia o RPC para o dono do CombatSystem2D
                            attackerView.RPC(nameof(CombatSystem2D.KillConfirmed), attackerView.Owner);
                    }
                }

                // 5. Destrói o objeto na rede (apenas o dono deve fazê-lo)
                PhotonNetwork.Destroy(gameObject);
            }
        }
    }

    // --- 3. LÓGICA DE CURA E UI ---

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