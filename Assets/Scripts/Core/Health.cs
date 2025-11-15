using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;

public class Health : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int maxHealth = 100;
    public int health = 100;
    public bool isLocalPlayer;

    public RectTransform healthBar;
    private float originalHealthBarsize;

    [Header("Knockback")]
    public float knockbackForceFallback = 10f;
    public float knockbackDurationFallback = 0.3f;

    private Rigidbody2D rb;
    private Movement2D playerMovement; // Requer o script Movement2D
    private bool isKnockedBack = false;
    private bool isDead = false;
    private PhotonView view;

    [Header("UI")]
    public TextMeshProUGUI healthText;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<Movement2D>();
        view = GetComponent<PhotonView>();
    }

    private void Start()
    {
        if (healthBar != null)
        {
            originalHealthBarsize = healthBar.sizeDelta.x;
        }
        health = Mathf.Clamp(health, 0, maxHealth);
        UpdateHealthUI();
    }

    // ---------------------------------------------------------------------------------
    // --- 1. MÉTODOS RPC DE DANO (Assinaturas Unificadas) ---
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// RPC de dano SIMPLES. Implementa a assinatura 'TakeDamage(Int32, Int32)'
    /// exigida pelo CombatSystem2D, e redireciona para o método completo.
    /// </summary>
    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID)
    {
        // Chama o método COMPLETO, enviando 0f para knockback/duration.
        // O código de knockback usará os valores de fallback (knockbackForceFallback).
        TakeDamageComplete(_damage, attackerViewID, 0f, 0f);
    }

    /// <summary>
    /// RPC de dano COMPLETO. Esta assinatura é chamada internamente e pelo EnemyAI.
    /// </summary>
    [PunRPC]
    public void TakeDamageComplete(int _damage, int attackerViewID, float attackerKnockbackForce, float attackerKnockbackDuration)
    {
        if (isDead) return;

        health = Mathf.Max(health - _damage, 0);

        UpdateHealthUI();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        // Lógica de Knockback (apenas para o jogador local)
        if (view.IsMine && attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                // Decide entre a força/duração enviada pelo atacante (se > 0) ou o fallback.
                float finalForce = (attackerKnockbackForce > 0) ? attackerKnockbackForce : knockbackForceFallback;
                float finalDuration = (attackerKnockbackDuration > 0) ? attackerKnockbackDuration : knockbackDurationFallback;

                ApplyKnockback(
                    attackerView.transform.position,
                    finalForce,
                    finalDuration
                );
            }
        }

        if (health <= 0)
        {
            isDead = true;
            HandleDeath(attackerViewID);
        }
    }

    // ---------------------------------------------------------------------------------
    // --- 2. LÓGICA DE KNOCKBACK ---
    // ---------------------------------------------------------------------------------

    public void ApplyKnockback(Vector3 attackerPosition, float force, float duration)
    {
        if (rb == null || playerMovement == null || isDead || isKnockedBack) return;

        Vector2 direction = (transform.position - attackerPosition).normalized;
        if (direction.y < 0.2f) direction.y = 0.2f;
        direction = direction.normalized;

        StartCoroutine(KnockbackRoutine(direction, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 direction, float force, float duration)
    {
        isKnockedBack = true;
        if (playerMovement != null) playerMovement.SetKnockbackState(true);

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(duration);

        if (playerMovement != null) playerMovement.SetKnockbackState(false);
        isKnockedBack = false;
    }

    // ---------------------------------------------------------------------------------
    // --- 3. LÓGICA DE MORTE E UI ---
    // ---------------------------------------------------------------------------------

    private void HandleDeath(int attackerViewID)
    {
        Debug.Log($"{gameObject.name} morreu!");

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
                // Chama o método KillConfirmed no CombatSystem2D do atacante.
                attackerView.RPC("KillConfirmed", attackerView.Owner);
            }
        }

        // 3. Destrói o objeto na rede
        PhotonNetwork.Destroy(gameObject);
    }

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
        if (healthBar != null && originalHealthBarsize > 0)
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
