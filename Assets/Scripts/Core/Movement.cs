using UnityEngine;
using Photon.Pun;
using System.Collections; 

[RequireComponent(typeof(Rigidbody2D))]
public class Movement2D : MonoBehaviourPunCallbacks
{
    [Header("Movimento")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;

    [Header("Pulo")]
    public float jumpForce = 10f;
    public int maxJumps = 2;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    [Header("Wall Check")]
    private bool isTouchingWall = false; 

    [Header("Knockback")]
    public bool isKnockedBack = false; 

    // --- VARIÁVEIS INTERNAS DO POWER UP ---
    private float defaultWalkSpeed;
    private float defaultSprintSpeed;
    private float defaultJumpForce;
    private Coroutine currentBuffRoutine; // Para gerir o tempo do buff
    // -------------------------------------

    // Propriedades de Acesso
    public float CurrentHorizontalSpeed => rb != null ? rb.linearVelocity.x : 0f;
    public bool IsGrounded => grounded;

    // --- Referências de Componentes e Singletons ---
    private Rigidbody2D rb;
    private bool sprinting;
    private bool grounded;
    private int jumpCount;
    private CombatSystem2D combatSystem; 
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private PhotonView pv; 
    
    // Referências estáticas/Singleton (Opções)
    private GameChat chatInstance; 


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        combatSystem = GetComponent<CombatSystem2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        pv = GetComponent<PhotonView>();
        
        // Tenta obter a instância do Chat, se existir.
        chatInstance = GameChat.instance; 

        // 1. GUARDAR OS VALORES ORIGINAIS NO INÍCIO (Valores base para o Reset)
        defaultWalkSpeed = walkSpeed;
        defaultSprintSpeed = sprintSpeed;
        defaultJumpForce = jumpForce;

        if (groundCheck == null)
            Debug.LogWarning("GroundCheck não atribuído no inspector! Adicione um objeto filho para o check.");

        // Se o Photon View existir E não for o jogador local, desativa.
        if (pv != null && !pv.IsMine)
        {
            enabled = false;
            return;
        }

        isKnockedBack = false;
    }

    // Método público para ser chamado pelo Health.cs
    public void SetKnockbackState(bool state)
    {
        isKnockedBack = state;
    }

    // ----------------------------------------------------
    // --- MÉTODOS DO POWER UP ---
    // ----------------------------------------------------

    /// <summary>
    /// Ativa um buff temporário de velocidade e salto.
    /// Chamado por um PowerUp.cs ou similar.
    /// </summary>
    public void ActivateSpeedJumpBuff(float speedMultiplier, float jumpMultiplier, float duration)
    {
        // Garante que só o dono local ativa o buff
        if (pv != null && !pv.IsMine) return; 
        
        // Se já houver um buff ativo, para o anterior e reseta os stats.
        if (currentBuffRoutine != null)
        {
            StopCoroutine(currentBuffRoutine);
            ResetStats(); 
        }

        // Inicia a nova corrotina
        currentBuffRoutine = StartCoroutine(BuffRoutine(duration, speedMultiplier, jumpMultiplier));
    }

    private IEnumerator BuffRoutine(float duration, float speedMult, float jumpMult)
    {
        // Aplica os multiplicadores aos valores base
        walkSpeed = defaultWalkSpeed * speedMult;
        sprintSpeed = defaultSprintSpeed * speedMult;
        jumpForce = defaultJumpForce * jumpMult;
        
        // Opcional: Adicionar efeitos visuais/sonoros aqui

        yield return new WaitForSeconds(duration);

        // O tempo acabou, resetar stats
        ResetStats();
        currentBuffRoutine = null;
    }

    private void ResetStats()
    {
        // Volta aos valores base
        walkSpeed = defaultWalkSpeed;
        sprintSpeed = defaultSprintSpeed;
        jumpForce = defaultJumpForce;

        // Opcional: Remover efeitos visuais/sonoros aqui
    }
    // ----------------------------------------------------


    void Update()
    {
        // BLOQUEIO 1: Multiplayer (Apenas o jogador local deve controlar)
        if (pv != null && !pv.IsMine) return;

        // ----------------------------------------------------
        // BLOQUEIO 2: ESTADOS DE JOGO (LOBBY, PAUSA, CHAT)
        // ----------------------------------------------------
        
        // A. Bloqueio do Lobby (Verifica se o LobbyManager existe E se está a bloquear)
        bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);

        if (lobbyBlocking)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; // Garante que o jogador para.
            }
            if (anim) anim.SetFloat("Speed", 0f);
            return; 
        }

        // B. Verificação de Chão e estados de bloqueio
        if (groundCheck != null)
        {
            grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        bool isDefending = (combatSystem != null && combatSystem.isDefending);
        
        // Bloqueio do Chat: (Opcional)
        bool isChatOpen = (chatInstance != null && chatInstance.IsChatOpen);
        
        // Bloqueio da Pausa: (Opcional)
        bool isPaused = (PMMM.instance != null && PMMM.IsPausedLocally); 

        // ** LÓGICA DE BLOQUEIO DO CONTROLE ** (Knockback, Pausa, Chat, Defesa)
        if (isKnockedBack || isPaused || isChatOpen || isDefending)
        {
            // Se estiver bloqueado (exceto por Knockback), garantir que o movimento horizontal para.
            if (rb != null && !isKnockedBack)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            
            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("Grounded", grounded);
                anim.SetBool("IsSprinting", false);
            }

            // Permite que a gravidade e o Knockback/Defesa continuem
            if (isKnockedBack || isDefending)
            {
                // Ignora o input de controle horizontal (WASD)
            }
            else
            {
                // Se for Pausa ou Chat, ignora o resto da lógica de INPUT
                return; 
            }
        }
        
        // LÓGICA DE RESET DE SALTO
        if (rb != null)
        {
            if (grounded && Mathf.Abs(rb.linearVelocity.y) <= 0.1f)
            {
                jumpCount = 0;
                isTouchingWall = false; 
            }
            else if (isTouchingWall && !grounded)
            {
                jumpCount = 0; 
            }
        }

        float move = 0f;

        // LÓGICA DE MOVIMENTO E SALTO (SÓ se NÃO estiver a defender)
        if (!isDefending)
        {
            // Movimento horizontal
            move = Input.GetAxisRaw("Horizontal");
            sprinting = Input.GetKey(KeyCode.LeftShift);

            float currentSpeed = sprinting ? sprintSpeed : walkSpeed;

            // Aplica a velocidade de movimento
            if (Mathf.Abs(move) > 0.05f)
            {
                rb.linearVelocity = new Vector2(move * currentSpeed, rb.linearVelocity.y);
            }
            else
            {
                // Se não houver input, parar o movimento horizontal
                if (!isTouchingWall || grounded)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }

            // Salto com W (duplo salto) ou barra de espaço
            bool jumpInput = Input.GetKeyDown(KeyCode.W) || Input.GetButtonDown("Jump");

            if (jumpInput && jumpCount < maxJumps)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpCount++;
            }

            // Flip do sprite
            if (spriteRenderer != null)
            {
                if (move > 0.05f)
                    spriteRenderer.flipX = false;
                else if (move < -0.05f)
                    spriteRenderer.flipX = true;
            }
        }

        // Atualizar Animator
        if (anim)
        {
            anim.SetFloat("Speed", isDefending ? 0f : Mathf.Abs(move));
            anim.SetBool("Grounded", grounded);
            bool isSprintAnim = !isDefending && sprinting && Mathf.Abs(move) > 0.05f;
            anim.SetBool("IsSprinting", isSprintAnim);
        }
    }

    // --- LÓGICA DE COLISÃO (Ground/Wall Check) ---
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            if (collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
    
                if (contact.normal.y > 0.5f)
                {
                    grounded = true;
                    jumpCount = 0;
                    isTouchingWall = false;
                }
                else if (Mathf.Abs(contact.normal.x) > 0.5f && contact.normal.y < 0.5f)
                {
                    isTouchingWall = true;
                }
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            if (collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
    
                if (contact.normal.y > 0.5f)
                {
                    grounded = true;
                    isTouchingWall = false;
                }
                
                if (Mathf.Abs(contact.normal.x) > 0.5f && contact.normal.y < 0.5f)
                {
                    isTouchingWall = true;
                }
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            isTouchingWall = false;
        }
    }
    
    // Gizmos
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
