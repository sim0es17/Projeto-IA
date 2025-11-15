using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;              // Necessário para Image
using TMPro;                       // Necessário para TextMeshProUGUI
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;   // <--- ALIAS CORRECTO

[RequireComponent(typeof(PhotonView))]
public class CombatSystem2D : MonoBehaviourPunCallbacks
{
    [Header("Ataque")]
    public int damage;
    public float attackRange = 1f;
    public float attackCooldown = 0.5f;
    public Transform attackPoint;
    public LayerMask enemyLayers;

    [Header("Score")]
    public int hitScore = 10;   // Pontos por ACERTO
    public int killScore = 50;  // Pontos extra por KILL

    [Header("Defesa")]
    public float defenseCooldown = 2f;
    [HideInInspector] public bool isDefending = false;

    [Header("VFX")]
    public GameObject hitVFX;

    [Header("UI Defesa")]
    public Image defenseIcon;             // Ícone do escudo (para alterar a transparência)
    public TextMeshProUGUI defenseText;   // Texto com o tempo restante

    private float nextAttackTime = 0f;
    private float nextDefenseTime = 0f;
    private Animator anim;
    private PhotonView photonView;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();

        // Este script só corre no jogador local (inputs / UI),
        // é ligado no PlayerSetup.IsLocalPlayer()
        enabled = false;
    }

    void Start()
    {
        anim = GetComponent<Animator>();

        // Tentar encontrar automaticamente a UI de defesa (caso venha em prefab)
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
        // --- ATAQUE (botão esquerdo rato) ---
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && !isDefending)
        {
            nextAttackTime = Time.time + attackCooldown;
            photonView.RPC(nameof(Attack), RpcTarget.All);
        }

        // --- DEFESA (botão direito rato) ---
        if (Input.GetMouseButtonDown(1) && Time.time >= nextDefenseTime && !isDefending)
        {
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, true);
        }

        if (Input.GetMouseButtonUp(1) && isDefending)
        {
            photonView.RPC(nameof(SetDefenseState), RpcTarget.All, false);
            nextDefenseTime = Time.time + defenseCooldown;
        }

        // Actualizar UI de defesa (cooldown)
        UpdateDefenseUI();
    }

    // Actualiza a opacidade do ícone e o texto do cooldown
    private void UpdateDefenseUI()
    {
        // Só o jogador local mexe na sua própria UI
        if (!photonView.IsMine) return;

        if (defenseIcon == null && defenseText == null) return;

        float remaining = nextDefenseTime - Time.time;

        // Está em cooldown (após largar a defesa)
        if (remaining > 0f && !isDefending)
        {
            if (defenseIcon != null)
            {
                var c = defenseIcon.color;
                c.a = 0.5f;        // semi-transparente enquanto está em cooldown
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
            // Cooldown pronto OU a defender
            if (defenseIcon != null)
            {
                var c = defenseIcon.color;
                c.a = 1f;
                defenseIcon.color = c;
            }

            if (defenseText != null)
                defenseText.text = string.Empty;
        }
    }

    // RPC de ataque (é chamado em todos, mas só o dono aplica lógica)
    [PunRPC]
    void Attack()
    {
        if (anim) anim.SetTrigger("Attack");

        // Só o dono decide quem levou dano e atribui score
        if (!photonView.IsMine) return;

        // VFX do ataque
        if (hitVFX != null && attackPoint != null)
        {
            GameObject vfx = PhotonNetwork.Instantiate(hitVFX.name, attackPoint.position, Quaternion.identity);
            StartCoroutine(DestroyVFX(vfx, 1f));
        }

        // Detecção de inimigos atingidos
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.gameObject == gameObject)
                continue;

            PhotonView targetView = enemy.GetComponent<PhotonView>();
            if (targetView == null || targetView.ViewID == photonView.ViewID)
                continue;

            CombatSystem2D targetCombat = enemy.GetComponent<CombatSystem2D>();
            Health targetHealth = enemy.GetComponent<Health>();
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();

            // Se não tiver nenhum componente de vida, ignora
            if (targetHealth == null && enemyHealth == null)
                continue;

            // Reduz dano se o alvo estiver a defender
            bool targetDefending = (targetCombat != null && targetCombat.isDefending);
            int finalDamage = targetDefending ? damage / 4 : damage;

            // Aplica dano (usa o fallback de knockback da Health, porque só enviamos damage + attackerId)
            if (targetHealth != null)
            {
                targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);
            }
            else // EnemyHealth
            {
                targetView.RPC(nameof(EnemyHealth.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);
            }

            // SCORE DE ACERTO
            PhotonNetwork.LocalPlayer.AddScore(hitScore);

            Debug.Log($"{gameObject.name} acertou {enemy.name} com {finalDamage} de dano (+{hitScore} score)");
        }
    }

    // Chamado pelo Health / EnemyHealth quando confirmam uma kill deste atacante
    [PunRPC]
    public void KillConfirmed()
    {
        if (!photonView.IsMine) return;

        int currentKills = 0;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Kills", out object value))
            currentKills = (int)value;

        currentKills++;

        Hashtable props = new Hashtable { { "Kills", currentKills } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // Pontos extra por KILL
        PhotonNetwork.LocalPlayer.AddScore(killScore);

        Debug.Log($"{gameObject.name} matou um inimigo! +1 kill (+{killScore} score)");
    }

    private IEnumerator DestroyVFX(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (vfx == null) yield break;

        PhotonView vfxView = vfx.GetComponent<PhotonView>();

        if (vfxView != null && vfxView.IsMine)
            PhotonNetwork.Destroy(vfx);
        else if (vfxView == null)
            Destroy(vfx);
    }

    [PunRPC]
    void SetDefenseState(bool state)
    {
        isDefending = state;

        if (anim)
            anim.SetBool("IsDefending", state);

        if (state)
            Debug.Log($"{gameObject.name} está a defender!");
        else
            Debug.Log($"{gameObject.name} parou de defender.");
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
