using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;
using ExitGames.Client.Photon;
using System.Collections.Generic;

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

    private float nextAttackTime = 0f;
    private float nextDefenseTime = 0f;
    private Animator anim;


    void Awake()
    {
        enabled = false;
    }

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {

        // Lógica de Ataque (Input lido apenas no jogador local)
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !isDefending)
        {
            nextAttackTime = Time.time + attackCooldown;
            // Chama o RPC (todos veem a animação, mas só o atacante calcula o dano)
            photonView.RPC(nameof(Attack), RpcTarget.All);
        }

        // --- LÓGICA DE DEFESA ---

        // Quando o jogador CARREGA no botão de defesa
        if (Input.GetMouseButtonDown(1) && Time.time >= nextDefenseTime && !isDefending)
        {
            // RPC para sincronizar o estado de defesa em todos
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, true);
        }

        // Quando o jogador LARGA o botão de defesa
        if (Input.GetMouseButtonUp(1) && isDefending)
        {
            // RPC para sincronizar o fim do estado de defesa
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
            nextDefenseTime = Time.time + defenseCooldown;
        }
    }

    [PunRPC]
    void Attack()
    {
        if (anim) anim.SetTrigger("Attack");

        // LÓGICA AUTORITÁRIA: Usa 'photonView.IsMine' para garantir que SÓ O ATACANTE (dono do view)
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

                if (targetView != null && targetView.ViewID != photonView.ViewID && targetHealth != null)
                {
                    bool enemyDefending = (targetCombat != null && targetCombat.isDefending);
                    int finalDamage = enemyDefending ? damage / 4 : damage;

                    // Chama TakeDamage no alvo (Todos veem o dano)
                    targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);

                    // Adicionar pontuação (só o atacante adiciona)
                    PhotonNetwork.LocalPlayer.AddScore(finalDamage);
                    if (RoomManager.instance != null)
                        RoomManager.instance.SetMashes();

                    Debug.Log($"{gameObject.name} acertou {enemy.name} com {finalDamage} de dano!");
                }
            }
        }
    }

    [PunRPC]
    public void KillConfirmed()
    {
        // Usa 'photonView.IsMine' para garantir que só o jogador que fez a Kill
        // atualiza o score e CustomProperties.
        if (!photonView.IsMine) return;

        PhotonNetwork.LocalPlayer.AddScore(100);

        int currentKills = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Kills"))
            currentKills = (int)PhotonNetwork.LocalPlayer.CustomProperties["Kills"];
        currentKills++;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "Kills", currentKills } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"{gameObject.name} matou um inimigo! +100 pontos e +1 kill");
    }

    private IEnumerator DestroyVFX(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null)
        {
            PhotonView vfxView = vfx.GetComponent<PhotonView>();
            // O atacante (IsMine) é o dono do VFX e deve destruí-lo
            if (vfxView != null && vfxView.IsMine)
                PhotonNetwork.Destroy(vfx);
            else if (vfxView == null)
                Destroy(vfx);
        }
    }

    [PunRPC]
    void SetDefenseState(bool state)
    {
        // Sincroniza o estado de defesa em todos os clientes
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
