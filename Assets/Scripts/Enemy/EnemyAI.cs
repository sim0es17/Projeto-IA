using UnityEngine;
using System.Collections;
using Photon.Pun;
using ExitGames.Client.Photon; // Necessário para Hashes (embora não usado diretamente aqui)

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
    public float edgeCheckDistance = 0.6f;
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
    public int attackDamage = 10;           // Dano que o inimigo causa
    public float attackCooldown = 1.0f;     // Tempo entre ataques do inimigo
    public Transform attackPoint;           // Ponto de origem do ataque do inimigo
    public LayerMask playerLayer;           // Camada do Jogador (NOVA CAMADA NECESSÁRIA)

    // --- Propriedades para acesso externo (EnemyHealth) ---
    // Isto é útil se tiveres um script separado para a vida do inimigo
    public float KnockbackForce => knockbackForce;
    public float StunTime => stunTime;

    // --- 3. VARIÁVEIS PRIVADAS ---

    private Transform playerTarget;
    private Rigidbody2D rb;
    private float nextAttackTime = 0f;
    private PhotonView photonView;
    private int direction = 1; // 1 (Direita), -1 (Esquerda)
    private bool isGrounded = false;

    // --- 4. FUNÇÕES BASE ---

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        photonView = GetComponent<PhotonView>();
    }

    void Start()
    {
        // A lógica de IA e spawns deve correr apenas no Master Client
        if (PhotonNetwork.IsMasterClient)
        {
            currentState = AIState.Patrol;

            // Procura o jogador (NOTA: Isto só encontra o primeiro. Em jogos multiplayer,
            // pode ser necessário encontrar o jogador mais próximo ou manter uma lista).
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }
        }
    }

    void Update()
    {
        // Apenas o Master Client controla a IA
        if (!PhotonNetwork.IsMasterClient) return;

        isGrounded = CheckGrounded();

        // Máquina de estados
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

        // Verifica a beira da plataforma
        RaycastHit2D edgeHit = Physics2D.Raycast(
            checkPos + new Vector3(direction * wallCheckDistancePatrol, 0, 0),
            Vector2.down,
            edgeCheckDistance,
            groundLayer
        );

        // Verifica a parede
        RaycastHit2D wallHit = Physics2D.Raycast(
            transform.position,
            checkDir,
            wallCheckDistancePatrol,
            groundLayer
        );

        if (edgeHit.collider == null || wallHit.collider != null)
        {
            direction *= -1;
            // FLIP do inimigo para a nova direção
            transform.localScale = new Vector3(direction, 1, 1);
        }

        // Transição para Chase
        if (playerTarget != null && Vector2.Distance(transform.position, playerTarget.position) < chaseRange)
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
        // Calcula a direção baseada no alvo
        float directionX = (targetPos.x > selfPos.x) ? 1 : -1;

        // FLIP do inimigo para virar para o jogador
        transform.localScale = new Vector3(directionX, 1, 1);

        // Transições
        if (distance <= attackRange)
        {
            currentState = AIState.Attack;
            return;
        }
        else if (distance > chaseRange * 1.5f) // Perde o jogador de vista
        {
            currentState = AIState.Patrol;
            return;
        }

        // Movimento e Salto
        rb.linearVelocity = new Vector2(directionX * moveSpeed, rb.linearVelocity.y);

        // Lógica de salto para ultrapassar paredes ou subir plataformas
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

        // Certifica-se que o inimigo está virado para o jogador durante o ataque
        float directionX = (playerTarget.position.x > transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(directionX, 1, 1);

        rb.linearVelocity = Vector2.zero;

        if (Time.time >= nextAttackTime)
        {
            DoAttack();
            nextAttackTime = Time.time + attackCooldown;
            // Após o ataque, espera um pouco e volta a perseguir
            StartCoroutine(WaitAndTransitionTo(AIState.Chase, 0.5f));
        }
    }

    void HandleStunned()
    {
        // Enquanto atordoado, o inimigo não faz nada (a lógica de movimento é tratada no ApplyKnockbackRPC)
    }


    // --- 6. FUNÇÕES DE COMBATE ---

    void DoAttack()
    {
        // 1. Deteção de acerto (Collision Check)
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);

        foreach (Collider2D player in hitPlayers)
        {
            PhotonView targetView = player.GetComponent<PhotonView>();
            Health playerHealth = player.GetComponent<Health>();
            CombatSystem2D playerCombat = player.GetComponent<CombatSystem2D>();

            if (targetView != null && playerHealth != null)
            {
                // Determinar se o jogador está a defender (simulação de redução de dano)
                bool playerDefending = (playerCombat != null && playerCombat.isDefending);
                int finalDamage = playerDefending ? attackDamage / 4 : attackDamage;

                // 2. Chamada de Dano pela Rede (RPC)
                // targetView.RPC: Chama a função em todos os clientes
                // nameof(Health.TakeDamage): Usa o nome da função no script Health.cs
                // finalDamage: O valor do dano
                // photonView.ViewID: O ID do atacante (deste inimigo)
                targetView.RPC(nameof(Health.TakeDamage), RpcTarget.All, finalDamage, photonView.ViewID);

                Debug.Log($"Inimigo atacou {player.name} com {finalDamage} de dano!");
            }
        }
    }

    /// <summary>
    /// Chamado por RPC (do EnemyHealth ou Player) no Master Client para aplicar Knockback e Stun.
    /// </summary>
    [PunRPC]
    public void ApplyKnockbackRPC(Vector2 direction, float force, float time)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Só o Master Client aplica a física

        currentState = AIState.Stunned;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        StartCoroutine(ResetStun(time));
    }

    // --- 7. UTILS & COROUTINES ---

    bool CheckGrounded()
    {
        // Usa o ponto de origem do inimigo para verificar se toca no chão
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

        // Se o inimigo ainda estiver atordoado, volta a perseguir
        if (currentState == AIState.Stunned)
        {
            currentState = AIState.Chase;
        }
    }

    IEnumerator WaitAndTransitionTo(AIState newState, float delay)
    {
        yield return new WaitForSeconds(delay);
        // Garante que só transiciona se ainda estiver no estado Attack
        if (currentState == AIState.Attack)
        {
            currentState = newState;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Desenha os raios de deteção no editor para facilitar a configuração
        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
    }
}