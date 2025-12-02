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

    [Header("UI")]
    public RectTransform healthBar;
    private float originalHealthBarsize;
    public TextMeshProUGUI healthText;

    [Header("Knockback")]
    public float knockbackForceFallback = 10f;
    public float knockbackDurationFallback = 0.3f;

    private Rigidbody2D rb;
    private Movement2D playerMovement;
    private bool isKnockedBack = false;
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
        if (healthBar != null) originalHealthBarsize = healthBar.sizeDelta.x;
        health = Mathf.Clamp(health, 0, maxHealth);
        UpdateHealthUI();
    }

    // ---------------------------------------------------------------------------------
    // --- 1. MÉTODOS RPC DE DANO ---
    // ---------------------------------------------------------------------------------

    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID)
    {
        TakeDamageComplete(_damage, attackerViewID, 0f, 0f);
    }

    [PunRPC]
    public void TakeDamageComplete(int _damage, int attackerViewID, float attackerKnockbackForce, float attackerKnockbackDuration)
    {
        if (isDead) return;

        health = Mathf.Max(health - _damage, 0);
        UpdateHealthUI();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        // Lógica de Knockback (apenas local)
        if (view.IsMine && attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                float force = (attackerKnockbackForce > 0) ? attackerKnockbackForce : knockbackForceFallback;
                float duration = (attackerKnockbackDuration > 0) ? attackerKnockbackDuration : knockbackDurationFallback;
                ApplyKnockback(attackerView.transform.position, force, duration);
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
    // --- 3. LÓGICA DE MORTE (HÍBRIDA: SINGLE & MULTIPLAYER) ---
    // ---------------------------------------------------------------------------------

    private void HandleDeath(int attackerViewID)
    {
        Debug.Log($"{gameObject.name} morreu!");

        // Só o dono trata da lógica
        if (!view.IsMine) return;

        // --- MODO SINGLE PLAYER (Prioridade) ---
        // Verifica se existe um GameManager na cena
        GameObject spGameManager = GameObject.Find("GameManager");
        if (spGameManager != null)
        {
            Debug.Log("Modo Single Player detetado.");
            // Tenta mostrar o DeathMenu localmente
            DeathMenu dm = FindObjectOfType<DeathMenu>();
            if (dm != null) dm.Show();

            // Destrói localmente
            Destroy(gameObject);
            return;
        }

        // --- MODO MULTIPLAYER ---
        if (PhotonNetwork.IsConnected)
        {
            // Tenta encontrar o RoomManager (Singleton ou Pesquisa)
            RoomManager manager = RoomManager.instance;

            if (manager == null)
            {
                // Fallback de segurança: procura na cena
                manager = FindObjectOfType<RoomManager>();
            }

            if (manager != null)
            {
                // Avisa o Manager (Ele decide Respawn ou Game Over)
                manager.HandleMyDeath();
            }
            else
            {
                Debug.LogError("ERRO CRÍTICO: RoomManager não encontrado! Impossível fazer respawn.");
            }

            // Atualiza Stats no Photon
            UpdateDeathStats();

            // Notifica Atacante
            if (attackerViewID != -1)
            {
                PhotonView attackerView = PhotonView.Find(attackerViewID);
                if (attackerView != null) attackerView.RPC("KillConfirmed", attackerView.Owner);
            }

            // Destrói na rede
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void UpdateDeathStats()
    {
        int currentDeaths = 0;
        if (view.Owner.CustomProperties.ContainsKey("Deaths"))
            currentDeaths = (int)view.Owner.CustomProperties["Deaths"];

        Hashtable props = new Hashtable { { "Deaths", currentDeaths + 1 } };
        view.Owner.SetCustomProperties(props);
    }

    // ---------------------------------------------------------------------------------
    // --- 4. CURA E UI ---
    // ---------------------------------------------------------------------------------

    [PunRPC]
    public void Heal(int amount)
    {
        if (isDead) return;
        health = Mathf.Clamp(health + amount, 0, maxHealth);
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (healthBar != null && originalHealthBarsize > 0)
            healthBar.sizeDelta = new Vector2(originalHealthBarsize * health / (float)maxHealth, healthBar.sizeDelta.y);
        if (healthText != null) healthText.text = health.ToString();
    }
}