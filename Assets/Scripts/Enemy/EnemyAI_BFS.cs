using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody2D), typeof(PhotonView))]
// HERANÇA: Herda de EnemyBase para funcionar com o EnemyHealth
public class EnemyAI_BFS : EnemyBase
{
    // --- 1. DEFINIÇÃO DE ESTADOS ---
    public enum AIState { Idle, Patrol, Chase, Attack, Stunned }

    [Header("Estado e Configuração")]
    public AIState currentState;
    public float chaseRange = 10f;
    public float attackRange = 1.5f;
    public float moveSpeed = 3f;
    public float jumpForce = 8f;

    [Header("Configuração BFS (Pathfinding)")]
    [Tooltip("Tamanho de cada quadrado da grelha virtual. 0.5 costuma funcionar bem.")]
    public float cellSize = 0.5f;     
    [Tooltip("Quantos nós ele verifica antes de desistir (performance).")]
    public int maxSearchSteps = 200;  
    [Tooltip("Tempo em segundos entre recalculos de caminho.")]
    public float pathUpdateRate = 0.25f; 
    [Tooltip("Layer do chão/paredes. OBRIGATÓRIO definir.")]
    public LayerMask obstacleLayer;   

    [Header("Combate")]
    public float knockbackForce = 10f;
    public float stunTime = 0.5f;
    public int attackDamage = 7;
    public float attackCooldown = 1.5f;
    public Transform attackPoint;
    public LayerMask playerLayer;

    // --- IMPLEMENTAÇÃO DO ENEMYBASE (Overrides) ---
    public override float KnockbackForce => knockbackForce;
    public override float StunTime => stunTime;

    // --- VARIÁVEIS PRIVADAS ---
    private Transform playerTarget;
    private Rigidbody2D rb;
    private PhotonView photonView;
    private SpriteRenderer spriteRenderer;
    private bool isGrounded;
    private float nextAttackTime = 0f;
    
    // Variáveis BFS
    private List<Vector2> currentPath = new List<Vector2>();
    private int currentPathIndex = 0;
    private float pathTimer = 0f;

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
            FindTarget();
        }
    }

    void Update()
    {
        // Apenas o Master Client processa a IA
        if (!PhotonNetwork.IsMasterClient) return;

        // Tenta encontrar o player se perdeu a referência
        if (playerTarget == null) FindTarget();

        isGrounded = CheckGrounded();

        switch (currentState)
        {
            case AIState.Patrol:
                HandlePatrol();
                break;
            case AIState.Chase:
                HandleChaseBFS(); // <--- LÓGICA DE PATHFINDING AQUI
                break;
            case AIState.Attack:
                HandleAttack();
                break;
            case AIState.Stunned:
                // Não faz nada, espera o Stun acabar
                break;
        }
    }

    // --- 2. LÓGICA BFS (Breadth-First Search) ---

    void HandleChaseBFS()
    {
        if (playerTarget == null)
        {
            currentState = AIState.Patrol;
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);

        // A. Transições de Estado
        if (distToPlayer <= attackRange)
        {
            currentState = AIState.Attack;
            rb.linearVelocity = Vector2.zero; // Para o movimento
            return;
        }
        if (distToPlayer > chaseRange * 1.3f) // Margem para parar de perseguir
        {
            currentState = AIState.Patrol;
            return;
        }

        // B. Recalcular Caminho (Timer)
        pathTimer += Time.deltaTime;
        if (pathTimer >= pathUpdateRate)
        {
            pathTimer = 0;
            RunBFS(transform.position, playerTarget.position);
        }

        // C. Seguir o Caminho
        if (currentPath != null && currentPath.Count > 0)
        {
            // Se ainda há nós no caminho
            if (currentPathIndex < currentPath.Count)
            {
                Vector2 targetNode = currentPath[currentPathIndex];
                
                // Move para o nó atual
                MoveTowardsNode(targetNode);

                // Se chegou perto do nó (0.2f de tolerância), passa para o próximo
                if (Vector2.Distance(transform.position, targetNode) < 0.3f)
                {
                    currentPathIndex++;
                }
            }
            else
            {
                // Acabou o caminho (está no último nó), vai direto para o player
                MoveTowardsNode(playerTarget.position);
            }
        }
        else
        {
            // Fallback: Se o BFS falhar, tenta ir direto
            MoveTowardsNode(playerTarget.position);
        }
    }

    // O CORAÇÃO DO ALGORITMO
    void RunBFS(Vector2 startPos, Vector2 targetPos)
    {
        Vector2Int startNode = WorldToGrid(startPos);
        Vector2Int targetNode = WorldToGrid(targetPos);

        if (startNode == targetNode) return;

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        frontier.Enqueue(startNode);

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        cameFrom[startNode] = startNode; 

        bool found = false;
        int steps = 0;

        while (frontier.Count > 0 && steps < maxSearchSteps)
        {
            Vector2Int current = frontier.Dequeue();
            steps++;

            if (current == targetNode)
            {
                found = true;
                break;
            }

            foreach (Vector2Int next in GetNeighbors(current))
            {
                if (!cameFrom.ContainsKey(next))
                {
                    frontier.Enqueue(next);
                    cameFrom[next] = current;
                }
            }
        }

        if (found)
        {
            ReconstructPath(cameFrom, startNode, targetNode);
        }
    }

    List<Vector2Int> GetNeighbors(Vector2Int center)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        // Verifica Cima, Baixo, Esquerda, Direita
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in dirs)
        {
            Vector2Int neighbor = center + dir;
            
            // Verifica colisão no centro do "quadrado" vizinho
            Vector2 worldPos = GridToWorld(neighbor);
            // Raio pequeno para ver se o centro da célula está livre
            if (!Physics2D.OverlapCircle(worldPos, cellSize / 3f, obstacleLayer))
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    void ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int end)
    {
        currentPath.Clear();
        Vector2Int current = end;

        while (current != start)
        {
            currentPath.Add(GridToWorld(current));
            current = cameFrom[current];
        }
        
        currentPath.Reverse(); // O caminho vem do fim para o início, invertemos
        currentPathIndex = 0;
    }

    // Conversores Grelha <-> Mundo
    Vector2Int WorldToGrid(Vector2 pos) => new Vector2Int(Mathf.RoundToInt(pos.x / cellSize), Mathf.RoundToInt(pos.y / cellSize));
    Vector2 GridToWorld(Vector2Int gridPos) => new Vector2(gridPos.x * cellSize, gridPos.y * cellSize);

    // --- 3. MOVIMENTAÇÃO ---

    void MoveTowardsNode(Vector2 targetPos)
    {
        float dirX = 0;

        // Zona morta de 0.1f para evitar tremer
        if (targetPos.x > transform.position.x + 0.1f) dirX = 1;
        else if (targetPos.x < transform.position.x - 0.1f) dirX = -1;

        rb.linearVelocity = new Vector2(dirX * moveSpeed, rb.linearVelocity.y);

        if (dirX != 0) FlipSprite(dirX);

        // Lógica de Pulo: Se o alvo está acima e estamos no chão
        if (targetPos.y > transform.position.y + 0.5f && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    // --- 4. ESTADOS SIMPLES ---

    void HandlePatrol()
    {
        // Patrulha simples: Se ver player perto, persegue
        if (playerTarget != null && Vector2.Distance(transform.position, playerTarget.position) < chaseRange)
        {
            currentState = AIState.Chase;
        }
    }

    void HandleAttack()
    {
        rb.linearVelocity = Vector2.zero; // Trava no ataque

        if (playerTarget == null) 
        {
            currentState = AIState.Patrol;
            return;
        }

        if (Vector2.Distance(transform.position, playerTarget.position) > attackRange)
        {
            currentState = AIState.Chase; // Player fugiu
            return;
        }

        if (Time.time >= nextAttackTime)
        {
            DoAttack();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void DoAttack()
    {
        if (attackPoint == null) return;

        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);
        foreach (Collider2D hit in hitPlayers)
        {
            if (!hit.CompareTag("Player")) continue;

            PhotonView targetView = hit.GetComponentInParent<PhotonView>();
            CombatSystem2D playerCombat = hit.GetComponentInParent<CombatSystem2D>();
            
            if (targetView != null)
            {
                // Verifica defesa
                bool isDefending = (playerCombat != null && playerCombat.isDefending);
                int dmg = isDefending ? attackDamage / 4 : attackDamage;

                targetView.RPC("TakeDamageComplete", RpcTarget.All, dmg, photonView.ViewID, knockbackForce, stunTime);
            }
        }
    }

    // --- 5. OVERRIDES (RPCs) ---

    [PunRPC]
    public override void ApplyKnockbackRPC(Vector2 direction, float force, float time)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        currentState = AIState.Stunned;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);
        StartCoroutine(ResetStun(time));
    }

    IEnumerator ResetStun(float time)
    {
        yield return new WaitForSeconds(time);
        // Só volta a perseguir se ainda estiver atordoado (pode ter morrido entretanto)
        if (currentState == AIState.Stunned) currentState = AIState.Chase;
    }

    // --- 6. UTILS ---

    void FindTarget()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;
    }

    bool CheckGrounded()
    {
        return Physics2D.Raycast(transform.position, Vector2.down, 1.2f, obstacleLayer);
    }

    private void FlipSprite(float dir)
    {
        if (spriteRenderer != null) spriteRenderer.flipX = (dir < 0);
        if (attackPoint != null)
        {
            // Move o AttackPoint para a esquerda/direita
            float x = Mathf.Abs(attackPoint.localPosition.x) * (dir > 0 ? 1 : -1);
            attackPoint.localPosition = new Vector3(x, attackPoint.localPosition.y, 0);
        }
    }

    // --- DEBUG VISUAL ---
    void OnDrawGizmos()
    {
        // Desenha o caminho do BFS
        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i+1]);
            }
            Gizmos.DrawWireSphere(currentPath[currentPathIndex], 0.2f); // Próximo ponto
        }

        // Alcance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        // Ataque
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}
