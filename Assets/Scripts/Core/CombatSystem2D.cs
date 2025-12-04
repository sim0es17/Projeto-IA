using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using UnityEngine.UI; 
using TMPro; 
using Hashtable = ExitGames.Client.Photon.Hashtable; 

[RequireComponent(typeof(PhotonView))]
public class CombatSystem2D : MonoBehaviourPunCallbacks
{
    [Header("Ataque")]
    public int damage = 10;
    public float attackRange = 1f;
    public float attackCooldown = 0.5f;
    public Transform attackPoint;
    public LayerMask enemyLayers;

    [Header("Ataque Carregado")]
    public int chargedDamageMultiplier = 2;  // Multiplicador do dano base (ex: 10 * 2 = 20)
    public float chargeTime = 1f;            // Tempo necessário para carregar o ataque
    public int attackAnimLayer = 0;          // Layer do Animator para o ataque

    [Header("Defesa")]
    public float defenseCooldown = 2f;
    [HideInInspector] public bool isDefending = false;

    [Header("Knockback (Dano Player vs Player)")]
    public float pvpKnockbackForce = 5f; 
    public float pvpKnockbackDuration = 0.2f;

    [Header("VFX")]
    public GameObject hitVFX;

    // Elementos de UI
    [Header("UI Defesa")]
    public Image defenseIcon; 
    public TextMeshProUGUI defenseText; 

    private float nextAttackTime = 0f;
    private float nextDefenseTime = 0f;
    private Animator anim;
    private PhotonView photonView;
    
    // Variáveis para o carregamento e animação
    private float chargeStartTime = 0f;
    private bool isCharging = false;
    
    // Referência do GameChat para verificar o estado
    private GameChat chatInstance;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        
        // O script só deve rodar no jogador local para gerir os inputs.
        if (photonView != null && !photonView.IsMine)
        {
            enabled = false;
        }
    }

    void Start()
    {
        anim = GetComponent<Animator>();
        // Tenta obter a referência do Singleton do chat, se existir.
        chatInstance = GameChat.instance;

        // Lógica de procura de UI (Útil para prefabs)
        if (defenseIcon == null || defenseText == null)
        {
            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                if (defenseIcon == null)
                    defenseIcon = canvas.transform.Find("DefenseIcon")?.GetComponent<Image>();

                if (defenseText == null && defenseIcon != null)
                    defenseText = defenseIcon.transform.Find("DefenseCooldownText")?.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    void Update()
    {
        // 1. BLOQUEIO MULTIPLAYER
        if (photonView != null && !photonView.IsMine) return;

        // ----------------------------------------------------
        // 2. BLOQUEIO POR ESTADO DE JOGO (LOBBY, PAUSA, CHAT)
        // ----------------------------------------------------
        
        bool isChatActive = (chatInstance != null && chatInstance.IsChatOpen);
        bool isPaused = PMMM.IsPausedLocally; 
        bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);

        
        if (isPaused || isChatActive || lobbyBlocking)
        {
            // Força a desativação da defesa em qualquer estado de bloqueio
            if (isDefending)
            {
                if (photonView != null && PhotonNetwork.InRoom)
                {
                    photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
                }
                else
                {
                    SetDefenseState(false);
                }
            }
            // Se estiver a carregar o ataque, cancela.
            if (isCharging)
            {
                isCharging = false;
                if (photonView != null && PhotonNetwork.InRoom)
                {
                    // Desliga o estado de ataque e retoma a velocidade da animação para 1
                    photonView.RPC(nameof(SetAttackState), RpcTarget.All, false, 1f); 
                }
                else
                {
                    SetAttackState(false, 1f);
                }
            }

            return; // Bloqueia todo o input de combate (Ataque e Defesa)
        }

        // --- INPUT DE COMBATE (Lógica principal) ---
        
        // ----------------------------------------------------
        // Lógica de Ataque Carregado / Ataque Normal (Mouse 0)
        // ----------------------------------------------------
        
        // 1. INICIAR CARREGAMENTO (Mouse Down)
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !isDefending && !isCharging)
        {
            isCharging = true;
            chargeStartTime = Time.time;
            
            // Inicia o ataque em todos (RPC), mas com a animação parada (speed 0)
            if (photonView != null && PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(SetAttackState), RpcTarget.All, true, 0f); 
            }
            else
            {
                SetAttackState(true, 0f); // Chamada local
            }
        }

        // 2. EXECUTAR / CANCELAR (Mouse Up)
        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            isCharging = false;

            float chargeDuration = Time.time - chargeStartTime;
            bool isCharged = chargeDuration >= chargeTime;

            // Retoma a animação (speed 1) para terminar o ciclo
            if (photonView != null && PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(SetAttackState), RpcTarget.All, true, 1f); 
            }
            else
            {
                SetAttackState(true, 1f); // Chamada local
            }

            nextAttackTime = Time.time + attackCooldown;
            
            // Chama a lógica de dano APENAS no proprietário para calcular o dano.
            if (photonView == null || photonView.IsMine)
            {
                ApplyDamageAndVFX(isCharged); 
            }
        }

        // LÓGICA DE DEFESA (Mouse 1) - MANTIDA
        if (Input.GetMouseButtonDown(1) && Time.time >= nextDefenseTime && !isDefending && !isCharging)
        {
            if (photonView != null && PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(SetDefenseState), RpcTarget.All, true);
            }
            else
            {
                SetDefenseState(true); // Chamada local
            }
        }

        if (Input.GetMouseButtonUp(1) && isDefending)
        {
            if (photonView != null && PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
            }
            else
            {
                SetDefenseState(false); // Chamada local
            }
            nextDefenseTime = Time.time + defenseCooldown;
        }

        // Atualizar o estado visual do cooldown
        UpdateDefenseUI();
    }

    // --- Métodos de Controlo de Animação e Dano ---

    [PunRPC]
    void SetAttackState(bool state, float animSpeed)
    {
        // Executado em todos os clientes para sincronizar o estado da animação

        if (anim)
        {
            // O parâmetro "IsAttacking" será um Bool no Animator
            anim.SetBool("IsAttacking", state); 
            
            // Define a velocidade da animação (0 para pausar, 1 para retomar)
            anim.speed = animSpeed; 
        }

        // Se o estado for FALSE (fim da animação), redefinimos a velocidade
        if (!state)
        {
            if (anim) anim.speed = 1f;
        }
    }

    // Chamado via Animation Event no Frame final da animação de ataque.
    public void AttackEnd()
    {
        // Este evento deve ocorrer em todos os clientes para redefinir o estado.
        if (photonView != null && PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(SetAttackState), RpcTarget.All, false, 1f); // state=false, speed=1
        }
        else
        {
            SetAttackState(false, 1f); // Chamada local
        }
    }

    // Método que aplica o dano e cria VFX (Executado APENAS no proprietário)
    void ApplyDamageAndVFX(bool isCharged)
    {
        if (photonView == null || photonView.IsMine)
        {
            // 1. Cálculo do Dano
            int currentDamage = isCharged ? damage * chargedDamageMultiplier : damage;

            // Instanciar VFX
            if (hitVFX != null && attackPoint != null)
            {
                GameObject vfx;
                if (photonView != null && PhotonNetwork.InRoom)
                {
                    vfx = PhotonNetwork.Instantiate(hitVFX.name, attackPoint.position, Quaternion.identity);
                }
                else
                {
                    vfx = Instantiate(hitVFX, attackPoint.position, Quaternion.identity);
                }
                float vfxDuration = isCharged ? 1.5f : 1f;
                StartCoroutine(DestroyVFX(vfx, vfxDuration));
            }

            // Deteção e Cálculo de dano
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);
            foreach (Collider2D enemy in hitEnemies)
            {
                if (enemy.gameObject == gameObject) continue;

                PhotonView targetView = enemy.GetComponent<PhotonView>(); 
                CombatSystem2D targetCombat = enemy.GetComponent<CombatSystem2D>();

                Health targetHealth = enemy.GetComponent<Health>();
                EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>(); 

                if (targetHealth != null || enemyHealth != null)
                {
                    bool targetDefending = (targetCombat != null && targetCombat.isDefending);
                    int finalDamage = targetDefending ? currentDamage / 4 : currentDamage; 

                    if (targetHealth != null)
                    {
                        // Player vs Player/Self
                        if (targetView != null && targetView.ViewID != photonView.ViewID)
                        {
                            targetView.RPC("TakeDamageComplete", RpcTarget.All, finalDamage, photonView.ViewID, pvpKnockbackForce, pvpKnockbackDuration);
                        }
                        else if (targetView == null && targetHealth.gameObject != gameObject)
                        {
                            targetHealth.TakeDamageComplete(finalDamage, 0, pvpKnockbackForce, pvpKnockbackDuration);
                        }
                    }
                    else if (enemyHealth != null)
                    {
                        // Player vs Enemy
                        if (targetView != null)
                        {
                            targetView.RPC("TakeDamage", RpcTarget.All, finalDamage, photonView.ViewID);
                        }
                        else
                        {
                            enemyHealth.TakeDamage(finalDamage, 0); 
                        }
                    }

                    // Se estivermos em Multiplayer, atualiza a pontuação
                    if (photonView != null && PhotonNetwork.InRoom)
                    {
                        PhotonNetwork.LocalPlayer.AddScore(finalDamage);
                    }
                    Debug.Log($"{gameObject.name} acertou {enemy.name} com {finalDamage} de dano! (Carregado: {isCharged})");
                }
            }
        }
    }

    // --- Métodos de Suporte (UI, Kills, VFX e Defesa) ---

    // Implementação do método de UI - MANTIDA
    private void UpdateDefenseUI()
    {
        if (defenseIcon == null && defenseText == null) return;

        float remaining = nextDefenseTime - Time.time;

        if (remaining > 0f && !isDefending)
        {
            if (defenseIcon != null)
            {
                var c = defenseIcon.color;
                c.a = 0.5f; 
                defenseIcon.color = c;
            }

            if (defenseText != null)
            {
                int seconds = Mathf.CeilToInt(remaining);
                defenseText.text = seconds.ToString();
            }
        }
        else
        {
            if (defenseIcon != null)
            {
                var c = defenseIcon.color;
                c.a = isDefending ? 0.7f : 1f; 
                defenseIcon.color = c;
            }

            if (defenseText != null)
                defenseText.text = ""; 
        }
    }

    [PunRPC]
    public void KillConfirmed()
    {
        // Lógica de kills é tipicamente Multiplayer - MANTIDA
        if (photonView != null && !photonView.IsMine) return;

        if (photonView != null && PhotonNetwork.InRoom)
        {
            int currentKills = 0;
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Kills"))
                currentKills = (int)PhotonNetwork.LocalPlayer.CustomProperties["Kills"];
            currentKills++;

            Hashtable props = new Hashtable { { "Kills", currentKills } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            Debug.Log($"{gameObject.name} matou um inimigo! +1 kill.");
        }
    }

    private IEnumerator DestroyVFX(GameObject vfx, float delay)
    {
        // Corrotina para destruir o VFX - MANTIDA
        yield return new WaitForSeconds(delay);
        if (vfx == null) yield break;

        PhotonView vfxView = vfx.GetComponent<PhotonView>();
        
        if (vfxView != null && vfxView.IsMine)
        {
            PhotonNetwork.Destroy(vfx); 
        }
        else if (vfxView == null)
        {
            Destroy(vfx); 
        }
    }

    [PunRPC]
    void SetDefenseState(bool state)
    {
        // Executado em todos os clientes para sincronizar o estado - MANTIDA
        isDefending = state;

        if (anim)
        {
            anim.SetBool("IsDefending", state);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Desenha o raio de ataque - MANTIDO
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
