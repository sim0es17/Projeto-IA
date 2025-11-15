using UnityEngine;
using System.Collections;
using Photon.Pun;
using ExitGames.Client.Photon;

// Garante que o objeto inimigo tem estes componentes
[RequireComponent(typeof(Rigidbody2D), typeof(PhotonView))]
public class EnemyAI : MonoBehaviourPunCallbacks
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
    public float knockbackForce = 10f;       // Força de knockback que o inimigo APLICA
    public float stunTime = 0.5f;            // Duração do stun (pode ser usado como duração do knockback)
    public int attackDamage = 7;             // Dano que o inimigo causa
    public float attackCooldown = 1.5f;      // Tempo entre ataques do inimigo
    public float attackOffsetDistance = 0.5f; // Distância exata que o ponto de ataque deve estar do centro.

    public Transform attackPoint;            // Ponto de origem do ataque do inimigo (filho do Enemy)
    public LayerMask playerLayer;            // Camada do Jogador

    // Propriedades para acesso externo (EnemyHealth)
    public float KnockbackForce => knockbackForce;
    public float StunTime => stunTime;

    // --- 3. VARIÁVEIS PRIVADAS ---

    private Transform playerTarget;
    private Rigidbody2D rb;
    private float nextAttackTime = 0f;
    private PhotonView photonView;
    private int direction = 1; // 1 (Direita), -1 (Esquerda)
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

            // Tenta encontrar o player na cena
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 0)
            {
                // Simplificação: apenas encontra o primeiro jogador
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

        // Raycast para Abismo (Edge)
        RaycastHit2D edgeHit = Physics2D.Raycast(
            checkPos + new Vector3(direction * wallCheckDistancePatrol, 0, 0),
            Vector2.down,
            edgeCheckDistance,
            groundLayer
        );

        // Raycast para Parede (Wall)
        RaycastHit2D wallHit = Physics2D.Raycast(
            transform.position,
            checkDir,
            wallCheckDistancePatrol,
            groundLayer
        );

        bool atWallOrEdge = (edgeHit.collider == null || wallHit.collider != null);

        if (atWallOrEdge)
        {
            // Bateu na parede ou abismo, vira.
            direction *= -1;
        }
        else
        {
            // Verifica se atingiu o limite de patrulha.
            float distanceToOrigin = Mathf.Abs(transform.position.x - patrolOrigin.x);

            if (distanceToOrigin >= patrolDistance)
            {
                // Força a virar de volta para a origem.
                if (transform.position.x > patrolOrigin.x && direction == 1)
                {
                    direction = -1; // Vira para a esquerda (para a origem)
                }
                else if (transform.position.x < patrolOrigin.x && direction == -1)
                {
                    direction = 1; // Vira para a direita (para a origem)
                }
            }
        }

        FlipSprite(direction);

        if (CanSeePlayer())
        {
            currentState = AIState.Chase;
        }
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

        // Lógica de salto para paredes ou subir plataformas
        RaycastHit2D wallHit = Physics2D.Raycast(
            selfPos,
            new Vector2(directionX, 0),
            wallCheckDistanceChase,
            groundLayer
        );

        if (wallHit.collider != null)
        {
            RaycastHit2D heightHit = Physics2D.Raycast(
                selfPos + new Vector2(directionX * wallCheckDistanceChase, 0),
                Vector2.up,
                jumpHeightTolerance,
                groundLayer
            );

            if (heightHit.collider == null)
            {
                TryJump();
            }
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

    void HandleStunned()
    {
        // Apenas para manter o estado (a corrotina ResetStun fará a transição)
    }

    // --- FUNÇÃO DE FLIP AUXILIAR ---
    private void FlipSprite(float currentDirection)
    {
        if (spriteRenderer == null || attackPoint == null) return;

        // 1. Inverte o SpriteRenderer (Flip Visual)
        spriteRenderer.flipX = (currentDirection < 0);

        // 2. CORREÇÃO DO ATTACK POINT (Reposiciona o ataque para a frente)
        float newLocalX = attackOffsetDistance * Mathf.Sign(currentDirection);
        attackPoint.localPosition = new Vector3(newLocalX, attackPoint.localPosition.y, attackPoint.localPosition.z);
    }

    // --- 6. FUNÇÕES DE COMBATE ---

    void DoAttack()
    {
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);

        if (hitPlayers.Length == 0)
        {
            Debug.LogError($"[ERRO DE HIT] OverlapCircle não detetou NINGUÉM. Pos: {attackPoint.position}, Raio: {attackRange}, Layer: {playerLayer.value}");
        }

        foreach (Collider2D player in hitPlayers)
        {
            PhotonView targetView = player.GetComponent<PhotonView>();
            Health playerHealth = player.GetComponent<Health>();
            CombatSystem2D playerCombat = player.GetComponent<CombatSystem2D>();

            if (targetView != null && playerHealth != null)
            {
                bool playerDefending = (playerCombat != null && playerCombat.isDefending);
                int finalDamage = playerDefending ? attackDamage / 4 : attackDamage;

                // 🌟 CORREÇÃO CRÍTICA: Chama o método de 4 parâmetros 'TakeDamageComplete' 🌟
                targetView.RPC(
                    nameof(Health.TakeDamageComplete), // <--- CORREÇÃO AQUI!
                    RpcTarget.All,
                    finalDamage,
                    photonView.ViewID,
                    knockbackForce,       // Força de Knockback (float)
                    stunTime              // Duração do Knockback (float)
                );

                Debug.Log($"Inimigo atacou {player.name} com {finalDamage} de dano! Enviou knockback: {knockbackForce}");
            }
        }
    }

    [PunRPC]
    public void ApplyKnockbackRPC(Vector2 direction, float force, float time)
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
        if (distance > chaseRange)
        {
            return false;
        }

        RaycastHit2D hit = Physics2D.Linecast(selfPos, targetPos, groundLayer);

        return hit.collider == null;
    }

    bool CheckGrounded()
    {
        return Physics2D.Raycast(transform.position, Vector2.down, 0.1f, groundLayer);
    }

    void TryJump()
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    IEnumerator ResetStun(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);

        if (currentState == AIState.Stunned)
        {
            currentState = AIState.Chase;
        }
    }

    IEnumerator WaitAndTransitionTo(AIState newState, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentState == AIState.Attack)
        {
            currentState = newState;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Gizmo de Ataque
        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        // Gizmo de Perseguição
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        // Gizmo de Linha de Visão
        if (playerTarget != null)
        {
            Gizmos.color = CanSeePlayer() ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }

        // NOVO GIZMO PARA PATRULHA
        Gizmos.color = Color.cyan;
        Vector3 patrolStartPoint = Application.isPlaying ? patrolOrigin : transform.position;

        Vector3 leftPoint = patrolStartPoint + Vector3.left * patrolDistance;
        Vector3 rightPoint = patrolStartPoint + Vector3.right * patrolDistance;

        leftPoint.y = transform.position.y;
        rightPoint.y = transform.position.y;

        Gizmos.DrawLine(leftPoint, rightPoint);
        Gizmos.DrawWireSphere(leftPoint, 0.2f);
        Gizmos.DrawWireSphere(rightPoint, 0.2f);
    }
}
