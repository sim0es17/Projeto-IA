using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using UnityEngine.UI; // Necessário para a classe Image
using TMPro; // Necessário para TextMeshProUGUI

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

    [Header("VFX")]
    public GameObject hitVFX;

    // NOVO: Elementos de UI
    [Header("UI Defesa")]
    public Image defenseIcon; // Ícone do shield (para alterar a transparência)
    public TextMeshProUGUI defenseText; // Texto com o tempo restante

    private float nextAttackTime = 0f;
    private float nextDefenseTime = 0f;
    private Animator anim;
    private PhotonView photonView;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        // Este script só deve rodar no jogador local para gerir os inputs e UI
        enabled = false;
    }

    void Start()
    {
        anim = GetComponent<Animator>();

        // NOVO: Tentar encontrar UI automaticamente (útil para pré-fabs)
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
        // NOVO: VERIFICAÇÃO DE PAUSA
        // Bloqueia todo o input de combate se o menu de pausa estiver ativo.
        if (PMMM.IsPausedLocally)
        {
            // Se estivermos a defender e pausarmos, devemos forçar o fim da defesa.
            // Isto garante que o jogador não fica permanentemente defendendo se pausar e sair.
            if (isDefending)
            {
                photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
                // Não colocamos o cooldown aqui, pois o Update não vai correr de novo
                // até que o jogo seja retomado, e a lógica de cooldown deve ser aplicada no ResumeGame() ou OnMouseUp.
            }
            return; // IGNORA TODO O RESTO DA LÓGICA DE INPUT
        }

        // Lógica de Ataque (Input lido apenas no jogador local)
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !isDefending)
        {
            nextAttackTime = Time.time + attackCooldown;
            photonView.RPC(nameof(Attack), RpcTarget.All);
        }

        // --- LÓGICA DE DEFESA ---
        if (Input.GetMouseButtonDown(1) && Time.time >= nextDefenseTime && !isDefending)
        {
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, true);
        }

        if (Input.GetMouseButtonUp(1) && isDefending)
        {
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
            nextDefenseTime = Time.time + defenseCooldown;
        }

        // NOVO: Atualizar o estado visual do cooldown
        UpdateDefenseUI();
    }

    // NOVO: Implementação do método de UI
    private void UpdateDefenseUI()
    {
        // Só precisa de processar a UI se for o jogador local
        if (!photonView.IsMine) return;

        if (defenseIcon == null && defenseText == null) return;

        float remaining = nextDefenseTime - Time.time;

        // Está em cooldown (após largar a defesa)
        if (remaining > 0f && !isDefending)
        {
            // Diminui a opacidade do ícone para indicar "bloqueado"
            if (defenseIcon != null)
            {
                var c = defenseIcon.color;
                c.a = 0.5f;
                defenseIcon.color = c;
            }

            // Mostra o tempo restante
            if (defenseText != null)
            {
                int seconds = Mathf.CeilToInt(remaining);
                defenseText.text = seconds.ToString();
            }
        }
        else
        {
            // Cooldown pronto ou a defender
            if (defenseIcon != null)
            {
                var c = defenseIcon.color;
                c.a = 1f; // Volta a 100% de opacidade
                defenseIcon.color = c;
            }

            // Remove o número
            if (defenseText != null)
                defenseText.text = "";
        }
    }


    [PunRPC]
    void Attack()
    {
        if (anim) anim.SetTrigger("Attack");

        // LÓGICA AUTORITÁRIA
        if (photonView.IsMine)
        {
            // Instanciar VFX
            if (hitVFX != null && attackPoint != null)
            {
                GameObject vfx = PhotonNetwork.Instantiate(hitVFX.name, attackPoint.position, Quaternion.identity);
                StartCoroutine(DestroyVFX(vfx, 1f));
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

                if (targetView != null && targetView.ViewID != photonView.ViewID && (targetHealth != null || enemyHealth != null))
                {
                    bool targetDefending = (targetCombat != null && targetCombat.isDefending);
                    int finalDamage = targetDefending ? damage / 4 : damage;

                    if (targetHealth != null)
                    {
                        targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);
                    }
                    else if (enemyHealth != null)
                    {
                        targetView.RPC(nameof(EnemyHealth.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);
                    }

                    PhotonNetwork.LocalPlayer.AddScore(finalDamage);
                    Debug.Log($"{gameObject.name} acertou {enemy.name} com {finalDamage} de dano!");
                }
            }
        }
    }

    [PunRPC]
    public void KillConfirmed()
    {
        if (!photonView.IsMine) return;

        int currentKills = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Kills"))
            currentKills = (int)PhotonNetwork.LocalPlayer.CustomProperties["Kills"];
        currentKills++;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "Kills", currentKills } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"{gameObject.name} matou um inimigo! +1 kill.");
    }

    private IEnumerator DestroyVFX(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null)
        {
            PhotonView vfxView = vfx.GetComponent<PhotonView>();
            if (vfxView != null && vfxView.IsMine)
                PhotonNetwork.Destroy(vfx);
            else if (vfxView == null)
                Destroy(vfx);
        }
    }

    [PunRPC]
    void SetDefenseState(bool state)
    {
        isDefending = state;

        if (state)
        {
            if (anim) anim.SetBool("IsDefending", true);
            Debug.Log($"{gameObject.name} está defendendo!");
        }
        else
        {
            if (anim) anim.SetBool("IsDefending", false);
            Debug.Log($"{gameObject.name} parou de defender.");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
