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
    private Movement2D playerMovement; // Assumindo que você tem uma classe Movement2D
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

    // --- DANO (RPCs) ---
    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID) 
    { 
        // Chama a versão completa com valores de knockback padrão (0f)
        view.RPC("TakeDamageComplete", RpcTarget.All, _damage, attackerViewID, 0f, 0f);
    }

    [PunRPC]
    public void TakeDamageComplete(int _damage, int attackerViewID, float attackerKnockbackForce, float attackerKnockbackDuration)
    {
        if (isDead) return;
        health = Mathf.Max(health - _damage, 0);
        UpdateHealthUI();

        // Lógica de Knockback
        if (view.IsMine && attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                // Usa valores do atacante, se fornecidos, senão usa os de fallback.
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

    // --- KNOCKBACK ---
    public void ApplyKnockback(Vector3 attackerPosition, float force, float duration)
    {
        if (rb == null || playerMovement == null || isDead || isKnockedBack) return;
        
        Vector2 direction = (transform.position - attackerPosition).normalized;
        
        // --- CORREÇÃO APLICADA AQUI ---
        // Garante uma componente vertical mínima (0.1f) para evitar "rastejar" no chão, 
        // mas é baixa o suficiente para favorecer a repulsão horizontal.
        if (direction.y < 0.1f) direction.y = 0.1f; 
        
        StartCoroutine(KnockbackRoutine(direction.normalized, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 direction, float force, float duration)
    {
        isKnockedBack = true;
        if (playerMovement != null) playerMovement.SetKnockbackState(true);
        
        // Zera a velocidade atual para garantir que o impulso seja limpo
        rb.linearVelocity = Vector2.zero; 
        rb.AddForce(direction * force, ForceMode2D.Impulse);
        
        yield return new WaitForSeconds(duration);
        
        if (playerMovement != null) playerMovement.SetKnockbackState(false);
        isKnockedBack = false;
    }

    // --- MORTE (CORRIGIDO PARA ROOMMANAGER) ---
    private void HandleDeath(int attackerViewID)
    {
        Debug.Log($"{gameObject.name} morreu!");

        // Lógica de Respawn, tratada pelo RoomManager (só o jogador local a executa)
        if (view.IsMine)
        {
            if (RoomManager.instance != null)
            {
                RoomManager.instance.HandleMyDeath();
            }
            else
            {
                Debug.LogError("RoomManager não encontrado! Respawn impossível.");
            }
        }

        // Estatísticas e Killfeed (só o jogador local a executa)
        if (view.IsMine)
        {
            // 1. Atualiza o contador de Mortes (Deaths)
            int currentDeaths = 0;
            if (view.Owner.CustomProperties.ContainsKey("Deaths"))
                currentDeaths = (int)view.Owner.CustomProperties["Deaths"];
            Hashtable props = new Hashtable { { "Deaths", currentDeaths + 1 } };
            view.Owner.SetCustomProperties(props);

            // 2. Notifica o atacante para atualizar as Mortes (Kills)
            if (attackerViewID != -1)
            {
                PhotonView attackerView = PhotonView.Find(attackerViewID);
                if (attackerView != null) 
                {
                    // Chama KillConfirmed no Player Controller do atacante
                    attackerView.RPC("KillConfirmed", attackerView.Owner); 
                }
            }

            // 3. Destrói o objeto na rede
            PhotonNetwork.Destroy(gameObject);
        }
    }

    // --- CURA E UI ---
    [PunRPC]
    public void Heal(int amount) 
    { 
        if (!isDead) 
        { 
            health = Mathf.Clamp(health + amount, 0, maxHealth); 
            UpdateHealthUI(); 
        } 
    }

    private void UpdateHealthUI()
    {
        if (healthBar != null && originalHealthBarsize > 0)
            healthBar.sizeDelta = new Vector2(originalHealthBarsize * health / (float)maxHealth, healthBar.sizeDelta.y);
        
        if (healthText != null) healthText.text = health.ToString();
    }
}
