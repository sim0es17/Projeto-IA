using UnityEngine;
using System.Collections;
using Photon.Pun;
using ExitGames.Client.Photon;

// Garante que o objeto inimigo tem estes componentes
[RequireComponent(typeof(Rigidbody2D), typeof(PhotonView))]
// MUDANÇA 1: Herda de EnemyBase para funcionar com o EnemyHealth
public class EnemyAI : EnemyBase 
{
    // --- 1. DEFINIÇÃO DE ESTADOS ---
    public enum AIState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Stunned
    }

    // --- 2. VARIÁVEIS DE CONFIGURAÇÃO ---

    [Header("Geral")]
    public AIState currentState;
    public float chaseRange = 8f;
    public float attackRange = 1.5f;
    public float moveSpeed = 3f;

    [Header("Patrulha")]
    public float patrolSpeed = 1.5f;
    public float patrolDistance = 5f;
    public float edgeCheckDistance = 5f;
    public float wallCheckDistancePatrol = 0.5f;
    public Transform groundCheckPoint;
    public LayerMask groundLayer;

    [Header("Perseguição / Salto")]
    public float jumpForce = 8f;
    public float jumpHeightTolerance = 1.5f;
    public float minJumpDistance = 0.5f;
    public float wallCheckDistanceChase = 0.5f;

    [Header("Combate / Knockback")]
    public float knockbackForce = 10f;
    public float stunTime = 0.5f;
    public int attackDamage = 7;
    public float attackCooldown = 1.5f;
    public float attackOffsetDistance = 0.5f;

    public Transform attackPoint;
    public LayerMask playerLayer; // Layer Mask para o OverlapCircle (onde procura)

    // MUDANÇA 2: Adicionado 'override' para cumprir o contrato do EnemyBase
    public override float KnockbackForce => knockbackForce;
    public override float StunTime => stunTime;

    // --- 3. VARIÁVEIS PRIVADAS ---

    private Transform playerTarget;
    private Rigidbody2D rb;
    private float nextAttackTime = 0f;
    private PhotonView photonView;
    private int direction = 1;
    private Vector2 patrolOrigin;
    private bool isGrounded = false;
    private SpriteRenderer spriteRenderer;

    // --- 4. FUNÇÕES BASE ---

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        photonView = GetComponent<PhotonView>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            currentState = AIState.Patrol;
            patrolOrigin = transform.position;

            // Tenta encontrar o player (Atenção: isto só deve ser feito uma vez)
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 0)
            {
                playerTarget = players[0].transform;
            }
        }
    }

    void Update()
    {
        // Apenas o Master Client controla a IA
        if (!PhotonNetwork.IsMasterClient) return;

        isGrounded = CheckGrounded();

        switch (currentState)
        {
            case AIState.Patrol:
                HandlePatrol();
                break;
            case AIState.Chase:
                HandleChase();
                break;
            case AIState.Attack:
                HandleAttack();
                break;
            case AIState.Stunned:
                HandleStunned();
                break;
            default:
                // Usa rb.velocity se não estiveres no Unity 6 (rb.linearVelocity)
                rb.linearVelocity = Vector2.zero; 
                break;
        }
    }

    // --- 5. LÓGICA DE ESTADOS ---

    void HandlePatrol()
    {
        rb.linearVelocity = new Vector2(patrolSpeed * direction, rb.linearVelocity.y);

        Vector3 checkPos = groundCheckPoint.position;
        Vector2 checkDir = new Vector2(direction, 0);

        RaycastHit2D edgeHit = Physics2D.Raycast(checkPos + new Vector3(direction * wallCheckDistancePatrol, 0, 0), Vector2.down, edgeCheckDistance, groundLayer);
        RaycastHit2D wallHit = Physics2D.Raycast(transform.position, checkDir, wallCheckDistancePatrol, groundLayer);

        if (edgeHit.collider == null || wallHit.collider != null)
        {
            direction *= -1;
        }
        else
        {
            float distanceToOrigin = Mathf.Abs(transform.position.x - patrolOrigin.x);
            if (distanceToOrigin >= patrolDistance)
            {
                if (transform.position.x > patrolOrigin.x && direction == 1) direction = -1;
                else if (transform.position.x < patrolOrigin.x && direction == -1) direction = 1;
            }
        }

        FlipSprite(direction);

        if (CanSeePlayer()) currentState = AIState.Chase;
    }

    void HandleChase()
    {
        if (playerTarget == null) return;

        Vector2 targetPos = playerTarget.position;
        Vector2 selfPos = transform.position;
        float distance = Vector2.Distance(selfPos, targetPos);
        float directionX = (targetPos.x > selfPos.x) ? 1 : -1;

        FlipSprite(directionX);

        if (distance <= attackRange)
        {
            currentState = AIState.Attack;
            return;
        }
        else if (distance > chaseRange * 1.5f || !CanSeePlayer())
        {
            currentState = AIState.Patrol;
            return;
        }

        rb.linearVelocity = new Vector2(directionX * moveSpeed, rb.linearVelocity.y);

        // Lógica de salto simplificada
        RaycastHit2D wallHit = Physics2D.Raycast(selfPos, new Vector2(directionX, 0), wallCheckDistanceChase, groundLayer);
        if (wallHit.collider != null)
        {
            RaycastHit2D heightHit = Physics2D.Raycast(selfPos + new Vector2(directionX * wallCheckDistanceChase, 0), Vector2.up, jumpHeightTolerance, groundLayer);
            if (heightHit.collider == null) TryJump();
        }
        else if (targetPos.y > selfPos.y + 0.5f && distance > minJumpDistance)
        {
            TryJump();
        }
    }

    void HandleAttack()
    {
        if (playerTarget == null) return;

        float directionX = (playerTarget.position.x > transform.position.x) ? 1 : -1;
        FlipSprite(directionX);

        rb.linearVelocity = Vector2.zero;

        if (Time.time >= nextAttackTime)
        {
            DoAttack();
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(WaitAndTransitionTo(AIState.Chase, 0.5f));
        }
    }

    void HandleStunned() { }

    private void FlipSprite(float currentDirection)
    {
        if (spriteRenderer == null || attackPoint == null) return;
        spriteRenderer.flipX = (currentDirection < 0);
        float newLocalX = attackOffsetDistance * Mathf.Sign(currentDirection);
        attackPoint.localPosition = new Vector3(newLocalX, attackPoint.localPosition.y, attackPoint.localPosition.z);
    }

    // --- 6. FUNÇÕES DE COMBATE COM FILTRO DE TAG ---

    void DoAttack()
    {
        if (attackPoint == null)
        {
            Debug.LogError("⛔ [ERRO CRÍTICO] O AttackPoint não está atribuído no Inspector do Inimigo!");
            return;
        }

        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);

        foreach (Collider2D hit in hitPlayers)
        {
            if (!hit.CompareTag("Player")) continue;

            PhotonView targetView = hit.GetComponentInParent<PhotonView>();
            Health playerHealth = hit.GetComponentInParent<Health>();
            CombatSystem2D playerCombat = hit.GetComponentInParent<CombatSystem2D>();

            if (targetView != null && playerHealth != null)
            {
                bool playerDefending = (playerCombat != null && playerCombat.isDefending);
                int finalDamage = playerDefending ? attackDamage / 4 : attackDamage;

                targetView.RPC(
                    nameof(Health.TakeDamageComplete),
                    RpcTarget.All,
                    finalDamage,
                    photonView.ViewID,
                    knockbackForce,
                    stunTime
                );
            }
        }
    }

    // MUDANÇA 3: Adicionado 'override' para substituir o método abstrato do EnemyBase
    [PunRPC]
    public override void ApplyKnockbackRPC(Vector2 direction, float force, float time)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        currentState = AIState.Stunned;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);
        StartCoroutine(ResetStun(time));
    }

    // --- 7. UTILS & COROUTINES ---

    bool CanSeePlayer()
    {
        if (playerTarget == null) return false;
        Vector2 selfPos = transform.position;
        Vector2 targetPos = playerTarget.position;
        float distance = Vector2.Distance(selfPos, targetPos);
        if (distance > chaseRange) return false;
        RaycastHit2D hit = Physics2D.Linecast(selfPos, targetPos, groundLayer);
        return hit.collider == null;
    }

    bool CheckGrounded() => Physics2D.Raycast(transform.position, Vector2.down, 0.1f, groundLayer);

    void TryJump()
    {
        if (isGrounded) rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    IEnumerator ResetStun(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        if (currentState == AIState.Stunned) currentState = AIState.Chase;
    }

    IEnumerator WaitAndTransitionTo(AIState newState, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentState == AIState.Attack) currentState = newState;
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        if (playerTarget != null)
        {
            Gizmos.color = CanSeePlayer() ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }
    }
}
