using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;

[RequireComponent(typeof(PhotonView))]
public class CombatSystem2D : MonoBehaviourPunCallbacks
{
    [Header("Ataque")]
    public int damage;
    public float attackRange;
    public float attackCooldown;
    public Transform attackPoint;
    public LayerMask enemyLayers;

    [Header("Defesa")]
    public float defenseDuration;
    public float defenseCooldown;
    public bool isDefending = false;

    [Header("VFX")]
    public GameObject hitVFX;

    private float nextAttackTime = 0f;
    private float nextDefenseTime = 0f;
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();

        if (!photonView.IsMine)
            enabled = false;
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // ATAQUE — Botão esquerdo do rato
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !isDefending)
        {
            nextAttackTime = Time.time + attackCooldown;
            photonView.RPC(nameof(Attack), RpcTarget.All);
        }

        // DEFESA — Botão direito do rato
        if (Input.GetMouseButtonDown(1) && Time.time >= nextDefenseTime && !isDefending)
        {
            nextDefenseTime = Time.time + defenseCooldown;
            photonView.RPC(nameof(Defend), RpcTarget.All);
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
            if (enemy.gameObject == gameObject)
                continue;

            PhotonView targetView = enemy.GetComponent<PhotonView>();
            CombatSystem2D targetCombat = enemy.GetComponent<CombatSystem2D>();
            Health targetHealth = enemy.GetComponent<Health>();

            if (targetView != null && targetView.ViewID != photonView.ViewID)
            {
                bool enemyDefending = (targetCombat != null && targetCombat.isDefending);
                int finalDamage = enemyDefending ? damage / 4 : damage;

                // Envia dano ao inimigo
                targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);

                // Adiciona score conforme o tipo de dano
                if (photonView.IsMine)
                {
                    if (enemyDefending)
                        PhotonNetwork.LocalPlayer.AddScore(finalDamage / 2); // defesa parcial
                    else
                        PhotonNetwork.LocalPlayer.AddScore(finalDamage); // dano total
                }
                
                Debug.Log($"{gameObject.name} acertou {enemy.name} com {finalDamage} de dano!");
            }
        }
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
    void Defend()
    {
        if (anim) anim.SetTrigger("Defend");
        StartCoroutine(DefenseCoroutine());
        Debug.Log($"{gameObject.name} está defendendo!");
    }

    private IEnumerator DefenseCoroutine()
    {
        isDefending = true;
        yield return new WaitForSeconds(defenseDuration);
        isDefending = false;
    }

    // Chamada quando o jogador mata outro
    [PunRPC]
    public void RegisterKill()
    {
        if (photonView.IsMine)
        {
            PhotonNetwork.LocalPlayer.AddScore(100); // Pontos por kill
            Debug.Log($"{gameObject.name} ganhou 100 pontos por kill!");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
