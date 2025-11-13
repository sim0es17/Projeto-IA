//EnemyAI
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
    public float attackRange = 1f;
    public float moveSpeed = 3f;

    [Header("Patrulha")]
    public float patrolSpeed = 1.5f;
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
    public float knockbackForce = 15f;
    public float stunTime = 0.5f;
    public int attackDamage = 7;            // Dano que o inimigo causa
    public float attackCooldown = 1.5f;      // Tempo entre ataques do inimigo

    // NOVA VARIÁVEL: Define a distância exata que o ponto de ataque deve estar do centro.
    public float attackOffsetDistance = 0.5f;

    public Transform attackPoint;             // Ponto de origem do ataque do inimigo (filho do Enemy)
    public LayerMask playerLayer;             // Camada do Jogador

    // --- Propriedades para acesso externo (EnemyHealth) ---
    public float KnockbackForce => knockbackForce;
    public float StunTime => stunTime;

    // --- 3. VARIÁVEIS PRIVADAS ---

    private Transform playerTarget;
    private Rigidbody2D rb;
    private float nextAttackTime = 0f;
    private PhotonView photonView;
    private int direction = 1; // 1 (Direita), -1 (Esquerda) - Usado apenas para Patrulha
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

        RaycastHit2D edgeHit = Physics2D.Raycast(
            checkPos + new Vector3(direction * wallCheckDistancePatrol, 0, 0),
            Vector2.down,
            edgeCheckDistance,
            groundLayer
        );

        RaycastHit2D wallHit = Physics2D.Raycast(
            transform.position,
            checkDir,
            wallCheckDistancePatrol,
            groundLayer
        );

        if (edgeHit.collider == null || wallHit.collider != null)
        {
            direction *= -1;
        }

        FlipSprite(direction);

        // ** ALTERAÇÃO AQUI **: Verifica se pode ver o player antes de começar a perseguição.
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
        // ** ALTERAÇÃO AQUI **: Retorna ao Patrol se estiver demasiado longe OU se a linha de visão for bloqueada.
        else if (distance > chaseRange * 1.5f || !CanSeePlayer())
        {
            currentState = AIState.Patrol;
            return;
        }

        rb.linearVelocity = new Vector2(directionX * moveSpeed, rb.linearVelocity.y);

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
        // Apenas para manter o estado
    }

    // --- FUNÇÃO DE FLIP AUXILIAR (CORREÇÃO FINAL: Usa a nova variável attackOffsetDistance) ---
    private void FlipSprite(float currentDirection)
    {
        if (spriteRenderer == null || attackPoint == null) return;

        // 1. Inverte o SpriteRenderer (Flip Visual)
        spriteRenderer.flipX = (currentDirection < 0);

        // 2. CORREÇÃO DO ATTACK POINT (Reposiciona o ataque para a frente)

        // Usa o sinal da direção (1 ou -1) para determinar se a posição X é positiva ou negativa.
        float newLocalX = attackOffsetDistance * Mathf.Sign(currentDirection);

        // Aplica a nova posição LOCAL.
        attackPoint.localPosition = new Vector3(newLocalX, attackPoint.localPosition.y, attackPoint.localPosition.z);
    }

    // --- 6. FUNÇÕES DE COMBATE ---

    void DoAttack()
    {
        // O Physics2D.OverlapCircleAll usa a posição GLOBAL do attackPoint.
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);

        foreach (Collider2D player in hitPlayers)
        {
            PhotonView targetView = player.GetComponent<PhotonView>();
            Health playerHealth = player.GetComponent<Health>();
            CombatSystem2D playerCombat = player.GetComponent<CombatSystem2D>();

            if (targetView != null && playerHealth != null)
            {
                bool playerDefending = (playerCombat != null && playerCombat.isDefending);
                int finalDamage = playerDefending ? attackDamage / 4 : attackDamage;

                // Chamada de Dano pela Rede (RPC)
                targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);

                Debug.Log($"Inimigo atacou {player.name} com {finalDamage} de dano!");
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

    // ** NOVA FUNÇÃO **: Verifica a linha de visão usando Raycasting.
    bool CanSeePlayer()
    {
        if (playerTarget == null) return false;

        Vector2 selfPos = transform.position;
        Vector2 targetPos = playerTarget.position;

        // 1. Verificação de Distância (Rápida)
        float distance = Vector2.Distance(selfPos, targetPos);
        if (distance > chaseRange)
        {
            return false;
        }

        // 2. Verificação de Linha de Visão (Linecast)
        // Lança um raio da posição do inimigo até o jogador, mas SÓ verifica a 'groundLayer'
        RaycastHit2D hit = Physics2D.Linecast(selfPos, targetPos, groundLayer);

        // Se 'hit.collider' for null, significa que o raio não atingiu NENHUM objeto
        // na groundLayer no caminho, ou seja, a linha de visão está limpa.
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
        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        // Desenha a linha de visao quando o inimigo esta no modo Patrol
        if (playerTarget != null && currentState == AIState.Patrol)
        {
            Gizmos.color = CanSeePlayer() ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }
    }
}