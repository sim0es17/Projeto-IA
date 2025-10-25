using UnityEngine;
using Photon.Pun;
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

        // Safety: disable combat on remote players (will be re-enabled locally by PlayerSetup)
        if (!photonView.IsMine)
            enabled = false;
    }

    void Update()
    {
        // Apenas o jogador local pode atacar/defender
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

        // Só o dono instancia o VFX para evitar duplicados
        if (hitVFX != null && attackPoint != null && photonView.IsMine)
        {
            GameObject vfx = PhotonNetwork.Instantiate(hitVFX.name, attackPoint.position, Quaternion.identity);
            StartCoroutine(DestroyVFX(vfx, 1f));
        }

        // Detecta inimigos dentro do alcance
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            // Evita atacar a si próprio
            if (enemy.gameObject == gameObject)
                continue;

            PhotonView targetView = enemy.GetComponent<PhotonView>();
            CombatSystem2D targetCombat = enemy.GetComponent<CombatSystem2D>();

            // Garante que não atacamos objetos sem PhotonView ou o nosso próprio personagem
            if (targetView != null && targetView.ViewID != photonView.ViewID)
            {
                int finalDamage = (targetCombat != null && targetCombat.isDefending)
                    ? damage / 4
                    : damage;

                targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage);
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
    }

    private IEnumerator DefenseCoroutine()
    {
        isDefending = true;
        yield return new WaitForSeconds(defenseDuration);
        isDefending = false;
    }

    /*[PunRPC]
    public void TakeDamage(int dmg)
    {
        Debug.Log($"{gameObject.name} levou {dmg} de dano!");
        // Aqui pode chamar o script de vida, ex: GetComponent<Health>().TakeDamage(dmg);
    }*/

    // Mostra o alcance do ataque no editor
    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
