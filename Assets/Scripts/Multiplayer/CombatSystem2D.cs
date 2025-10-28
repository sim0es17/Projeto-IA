using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;
using ExitGames.Client.Photon;

[RequireComponent(typeof(PhotonView))]
public class CombatSystem2D : MonoBehaviourPunCallbacks
{
    [Header("Ataque")]
    public int damage = 25;
    public float attackRange = 1f;
    public float attackCooldown = 0.5f;
    public Transform attackPoint;
    public LayerMask enemyLayers;

    [Header("Defesa")]
    public float defenseCooldown = 2f;
    public bool isDefending = false;

    [Header("VFX")]
    public GameObject hitVFX;

    private float nextAttackTime = 0f;
    private float nextDefenseTime = 0f;
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (!photonView.IsMine) enabled = false;
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // Lógica de Ataque (sem alterações)
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !isDefending)
        {
            nextAttackTime = Time.time + attackCooldown;
            photonView.RPC(nameof(Attack), RpcTarget.All);
        }

        // --- LÓGICA DE DEFESA ---

        // 1. Quando o jogador CARREGA no botão de defesa
        if (Input.GetMouseButtonDown(1) && Time.time >= nextDefenseTime && !isDefending)
        {
            // Entra em modo de defesa
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, true);
        }

        // 2. Quando o jogador LARGA o botão de defesa
        if (Input.GetMouseButtonUp(1) && isDefending)
        {
            // Sai do modo de defesa
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);

            // O Cooldown SÓ COMEÇA quando o jogador larga a defesa
            nextDefenseTime = Time.time + defenseCooldown;
        }
    }

    [PunRPC]
    void Attack()
    {
        if (anim) anim.SetTrigger("Attack");

        if (hitVFX != null && attackPoint != null && photonView.IsMine)
        {
            GameObject vfx = PhotonNetwork.Instantiate(hitVFX.name, attackPoint.position, Quaternion.identity);
            StartCoroutine(DestroyVFX(vfx, 1f));
        }

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

                targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);

                if (photonView.IsMine)
                {
                    PhotonNetwork.LocalPlayer.AddScore(finalDamage);
                    if (RoomManager.instance != null)
                        RoomManager.instance.SetMashes();
                }

                Debug.Log($"{gameObject.name} acertou {enemy.name} com {finalDamage} de dano!");
            }
        }
    }

    [PunRPC]
    public void KillConfirmed()
    {
        if (!photonView.IsMine) return;

        // +100 pontos por kill
        PhotonNetwork.LocalPlayer.AddScore(100);

        // Atualiza kills nas CustomProperties
        int currentKills = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Kills"))
            currentKills = (int)PhotonNetwork.LocalPlayer.CustomProperties["Kills"];
        currentKills++;

        // Corrigido para evitar ambiguidade
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
            if (vfxView != null)
                PhotonNetwork.Destroy(vfx);
            else
                Destroy(vfx);
        }
    }

    [PunRPC]
    void SetDefenseState(bool state)
    {
        isDefending = state;

        if (state)
        {
            // Começou a defender
            if (anim) anim.SetBool("IsDefending", true); // Recomendo usar um Bool no Animator
            Debug.Log($"{gameObject.name} está defendendo!");
        }
        else
        {
            // Parou de defender
            if (anim) anim.SetBool("IsDefending", false); // Recomendo usar um Bool no Animator
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