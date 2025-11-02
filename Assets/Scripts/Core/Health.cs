using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
using ExitGames.Client.Photon;

public class Health : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int health = 100;
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

    private bool isDead = false; // <-- Manter esta flag. É importante.

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<Movement2D>();
    }

    private void Start()
    {
        originalHealthBarsize = healthBar.sizeDelta.x;
    }

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
        // 1. Desativa o controle do jogador
        playerMovement.SetKnockbackState(true);

        // 2. Aplica a força de impulso
        rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);

        // 3. Espera pela duração do knockback
        yield return new WaitForSeconds(knockbackDuration);

        // 4. Limpa a velocidade horizontal para evitar que o jogador deslize indefinidamente
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // 5. Reativa o controle do jogador
        playerMovement.SetKnockbackState(false);
    }

    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID = -1)
    {
        if (isDead) return; // Se já estiver morto, ignora dano adicional

        health -= _damage;

        healthBar.sizeDelta = new Vector2(originalHealthBarsize * health / 100f, healthBar.sizeDelta.y);

        if (healthText != null)
            healthText.text = health.ToString();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        // --- Lógica de Knockback (SOMENTE para o Local Player) ---
        if (isLocalPlayer && attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                // Aplica o knockback usando a posição do atacante
                ApplyKnockback(attackerView.transform.position);
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

                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "Deaths", currentDeaths } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);

                // 2. Fazer Respawn
                if (RoomManager.instance != null)
                    RoomManager.instance.RespawnPlayer();

                // 3. Notificar o atacante (LÓGICA MOVIDA PARA AQUI)
                if (attackerViewID != -1)
                {
                    PhotonView attackerView = PhotonView.Find(attackerViewID);
                    if (attackerView != null)
                    {
                        CombatSystem2D attackerCombat = attackerView.GetComponent<CombatSystem2D>();
                        if (attackerCombat != null)
                            attackerView.RPC(nameof(CombatSystem2D.KillConfirmed), attackerView.Owner);
                    }
                }
            }

            // Destroi o objeto morto (somente dono)
            PhotonView view = GetComponent<PhotonView>();
            if (view != null && view.IsMine)
                PhotonNetwork.Destroy(gameObject);
        }
    }
}
