using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using UnityEngine.UI; 
using TMPro; 
using Hashtable = ExitGames.Client.Photon.Hashtable; // Alias para a classe Hashtable do Photon

[RequireComponent(typeof(PhotonView))]
public class CombatSystem2D : MonoBehaviourPunCallbacks
{
    [Header("Ataque")]
    public int damage;
    public float attackRange = 1f;
    public float attackCooldown = 0.5f;
    public Transform attackPoint;
    public LayerMask enemyLayers;

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
    
    // Referência do GameChat para verificar o estado (pode ser null no SP)
    private GameChat chatInstance;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        
        // O script só deve rodar no jogador local para gerir os inputs.
        // Se o pv for null (cenas SP sem Network Manager), roda.
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

        // Tentar encontrar UI automaticamente (útil para pré-fabs)
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
        // BLOQUEIO 1: Multiplayer (Apenas o jogador local deve controlar)
        if (photonView != null && !photonView.IsMine) return;

        // ----------------------------------------------------
        // BLOQUEIO 2: ESTADOS DE JOGO (LOBBY, PAUSA, CHAT)
        // ----------------------------------------------------
        
        // Bloqueio do Chat (Opcional)
        bool isChatActive = (chatInstance != null && chatInstance.IsChatOpen);
        
        // Bloqueio da Pausa (Opcional)
        bool isPaused = (PMMM.instance != null && PMMM.IsPausedLocally); 
        
        // Bloqueio do Lobby (Opcional - só bloqueia se o LobbyManager existir E não tiver começado)
        bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);


        if (isPaused || isChatActive || lobbyBlocking)
        {
            // Se estivermos em pausa/chat/lobby, paramos de defender.
            if (isDefending)
            {
                // Se o photonView existir (MP), usa RPC. Se for null (SP), chama localmente.
                if (photonView != null)
                {
                    photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
                }
                else
                {
                    SetDefenseState(false);
                }
            }
            return; // Bloqueia todo o input de combate
        }

        // Lógica de Ataque (Input lido apenas no jogador local)
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !isDefending)
        {
            nextAttackTime = Time.time + attackCooldown;
            // Se o photonView existir (MP), usa RPC. Se for null (SP), chama localmente.
            if (photonView != null)
            {
                photonView.RPC(nameof(Attack), RpcTarget.All);
            }
            else
            {
                Attack();
            }
        }

        // --- LÓGICA DE DEFESA ---
        if (Input.GetMouseButtonDown(1) && Time.time >= nextDefenseTime && !isDefending)
        {
            if (photonView != null)
            {
                photonView.RPC(nameof(SetDefenseState), RpcTarget.All, true);
            }
            else
            {
                SetDefenseState(true);
            }
        }

        if (Input.GetMouseButtonUp(1) && isDefending)
        {
            if (photonView != null)
            {
                photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
            }
            else
            {
                SetDefenseState(false);
            }
            nextDefenseTime = Time.time + defenseCooldown;
        }

        // Atualizar o estado visual do cooldown (apenas UI local)
        UpdateDefenseUI();
    }

    // Implementação do método de UI (Inalterado)
    private void UpdateDefenseUI()
    {
        if (defenseIcon == null && defenseText == null) return;
        // ... (Lógica de UI) ...
        float remaining = nextDefenseTime - Time.time;

        if (remaining > 0f && !isDefending)
        {
            if (defenseIcon != null)
            {
                var c = defenseIcon.color;
                c.a = 0.5f; // Semitransparente em cooldown
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
                c.a = isDefending ? 0.7f : 1f; // Opaco ou ligeiramente transparente se a defender
                defenseIcon.color = c;
            }

            if (defenseText != null)
                defenseText.text = ""; // Limpa o texto
        }
    }


    [PunRPC] // O atributo RPC é necessário mesmo que seja chamado localmente em SP
    void Attack()
    {
        if (anim) anim.SetTrigger("Attack");

        // Esta lógica deve correr se for o jogador local (pv.IsMine) OU se não houver PV (SP)
        if (photonView == null || photonView.IsMine)
        {
            // Instanciar VFX
            if (hitVFX != null && attackPoint != null)
            {
                // Se for MP, instancia na rede. Se for SP, instancia localmente.
                GameObject vfx;
                if (photonView != null && PhotonNetwork.InRoom)
                {
                    vfx = PhotonNetwork.Instantiate(hitVFX.name, attackPoint.position, Quaternion.identity);
                }
                else
                {
                    vfx = Instantiate(hitVFX, attackPoint.position, Quaternion.identity);
                }
                StartCoroutine(DestroyVFX(vfx, 1f));
            }

            // Deteção e Cálculo de dano (Apenas o proprietário faz o cálculo de dano)
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);
            foreach (Collider2D enemy in hitEnemies)
            {
                if (enemy.gameObject == gameObject) continue;

                // Tenta obter o PV do alvo. Se for null (alvo é SP), o targetView é null.
                PhotonView targetView = enemy.GetComponent<PhotonView>(); 
                CombatSystem2D targetCombat = enemy.GetComponent<CombatSystem2D>();

                Health targetHealth = enemy.GetComponent<Health>();
                EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>(); 

                if (targetHealth != null || enemyHealth != null) // Se o alvo tem um componente de vida
                {
                    bool targetDefending = (targetCombat != null && targetCombat.isDefending);
                    int finalDamage = targetDefending ? damage / 4 : damage; 

                    if (targetHealth != null)
                    {
                        // Player vs Player/Self
                        if (targetView != null && targetView.ViewID != photonView.ViewID)
                        {
                            // MP: Chama RPC com parâmetros de Knockback no alvo
                            targetView.RPC("TakeDamageComplete", RpcTarget.All, finalDamage, photonView.ViewID, pvpKnockbackForce, pvpKnockbackDuration);
                        }
                        else if (targetView == null && targetHealth.gameObject != gameObject)
                        {
                            // SP: Chama o método localmente no alvo
                            targetHealth.TakeDamageComplete(finalDamage, 0, pvpKnockbackForce, pvpKnockbackDuration);
                        }
                    }
                    else if (enemyHealth != null)
                    {
                        // Player vs Enemy
                        if (targetView != null)
                        {
                            // MP: Chama RPC no inimigo
                            targetView.RPC("TakeDamage", RpcTarget.All, finalDamage, photonView.ViewID);
                        }
                        else
                        {
                            // SP: Chama o método localmente no inimigo
                            enemyHealth.TakeDamage(finalDamage, 0); 
                        }
                    }

                    // Se estivermos em Multiplayer, atualiza a pontuação local
                    if (photonView != null && PhotonNetwork.InRoom)
                    {
                        PhotonNetwork.LocalPlayer.AddScore(finalDamage);
                    }
                    Debug.Log($"{gameObject.name} acertou {enemy.name} com {finalDamage} de dano!");
                }
            }
        }
    }

    [PunRPC]
    public void KillConfirmed()
    {
        // A lógica de kills é tipicamente Multiplayer
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
        yield return new WaitForSeconds(delay);
        if (vfx == null) yield break;

        // Lógica de destruição segura para SP e MP
        PhotonView vfxView = vfx.GetComponent<PhotonView>();
        
        if (vfxView != null && vfxView.IsMine)
        {
            PhotonNetwork.Destroy(vfx); // Destruir na rede (MP)
        }
        else if (vfxView == null)
        {
            Destroy(vfx); // Destruir localmente (SP ou objetos locais em MP)
        }
    }

    [PunRPC]
    void SetDefenseState(bool state)
    {
        // Executado em todos os clientes para sincronizar o estado
        isDefending = state;

        if (anim)
        {
            anim.SetBool("IsDefending", state);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        // Desenha a área de ataque no editor para visualização
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
