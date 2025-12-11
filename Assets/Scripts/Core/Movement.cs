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
    // NOVO: Prefab do VFX de Salto (Arrastar aqui no Inspector)
    public GameObject jumpVFXPrefab; 

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    [Header("Wall Check")]
    private bool isTouchingWall = false;

    [Header("Knockback")]
    public bool isKnockedBack = false;

    // --- VARIÁVEIS INTERNAS ---
    private float defaultWalkSpeed;
    private float defaultSprintSpeed;
    private float defaultJumpForce;
    private Coroutine currentBuffRoutine;

    public float CurrentHorizontalSpeed => rb != null ? rb.linearVelocity.x : 0f;
    public bool IsGrounded => grounded;

    private Rigidbody2D rb;
    private bool sprinting;
    private bool grounded;
    private int jumpCount;
    private CombatSystem2D combatSystem; // Referência ao CombatSystem2D
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private PhotonView pv;

    // Referência do Singleton do Chat (Usada para bloquear o input)
    private GameChat chatInstance;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        combatSystem = GetComponent<CombatSystem2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        pv = GetComponent<PhotonView>();

        chatInstance = GameChat.instance; 
        if (chatInstance == null)
        {
            chatInstance = FindObjectOfType<GameChat>();
        }

        // 1. GUARDAR OS VALORES ORIGINAIS (Base para Reset)
        defaultWalkSpeed = walkSpeed;
        defaultSprintSpeed = sprintSpeed;
        defaultJumpForce = jumpForce;

        if (groundCheck == null)
            Debug.LogWarning("GroundCheck não atribuído no inspector!");

        // Se não for o dono (Multiplayer), desativa inputs
        if (pv != null && !pv.IsMine)
        {
            enabled = false;
            return;
        }

        isKnockedBack = false;
    }

    public void SetKnockbackState(bool state)
    {
        isKnockedBack = state;
    }

    // --- MÉTODOS DO POWER UP (O resto dos métodos de Buff/Reset são mantidos) ---
    
    [PunRPC]
    public void BoostSpeed(float boostAmount, float duration)
    {
        if (currentBuffRoutine != null)
        {
            StopCoroutine(currentBuffRoutine);
            ResetStats();
        }
        currentBuffRoutine = StartCoroutine(SpeedBuffRoutine(boostAmount, duration));
    }

    private IEnumerator SpeedBuffRoutine(float boostAmount, float duration)
    {
        walkSpeed += boostAmount;
        sprintSpeed += boostAmount;

        Debug.Log($"[Multiplayer] Velocidade aumentada!");

        yield return new WaitForSeconds(duration);

        ResetStats();
        currentBuffRoutine = null;
    }
    
    public void ActivateSpeedJumpBuff(float speedMultiplier, float jumpMultiplier, float duration)
    {
        if (pv != null && !pv.IsMine) return;

        if (currentBuffRoutine != null)
        {
            StopCoroutine(currentBuffRoutine);
            ResetStats();
        }

        currentBuffRoutine = StartCoroutine(BuffRoutineMultipliers(duration, speedMultiplier, jumpMultiplier));
    }

    private IEnumerator BuffRoutineMultipliers(float duration, float speedMult, float jumpMult)
    {
        walkSpeed = defaultWalkSpeed * speedMult;
        sprintSpeed = defaultSprintSpeed * speedMult;
        jumpForce = defaultJumpForce * jumpMult;

        Debug.Log("[SinglePlayer] Buff ativado!");

        yield return new WaitForSeconds(duration);

        ResetStats();
        currentBuffRoutine = null;
    }

    private void ResetStats()
    {
        walkSpeed = defaultWalkSpeed;
        sprintSpeed = defaultSprintSpeed;
        jumpForce = defaultJumpForce;
    }

    // NOVO MÉTODO: Criação do VFX de Salto
    private void SpawnJumpVFX()
    {
        if (pv != null && pv.IsMine)
        {
            // Chamamos a RPC para que todos os clientes vejam o efeito
            pv.RPC("SpawnJumpVFX_RPC", RpcTarget.All);
        }
    }

    // NOVA RPC: Executada em todos os clientes
    [PunRPC]
    private void SpawnJumpVFX_RPC()
    {
        if (jumpVFXPrefab != null && groundCheck != null)
        {
            // Instanciar o VFX na posição do Ground Check (onde o salto ocorre)
            GameObject vfx = Instantiate(jumpVFXPrefab, groundCheck.position, Quaternion.identity);
            
            // Opcional: Destruir o VFX após 2 segundos (ajuste conforme a duração do seu Particle System)
            Destroy(vfx, 2f); 
        }
        else
        {
            Debug.LogWarning("Jump VFX Prefab ou GroundCheck não atribuído! Não foi possível spawnar o VFX.");
        }
    }
    // ----------------------------------------------------

    void Update()
    {
        if (pv != null && !pv.IsMine) return;

        // Ground Check
        if (groundCheck != null)
        {
            grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        // --- GATHER BLOCKING STATES ---
        bool isDefending = (combatSystem != null && combatSystem.isDefending);
        
        // NOVO: Verifica se o ataque está a ser carregado (usando o bool público do CombatSystem)
        bool isChargingAttack = (combatSystem != null && combatSystem.isCharging);
        
        // Define o estado de Bloqueio de Movimento (Defesa OU Carregamento)
        bool isMovementBlocked = isDefending || isChargingAttack; 

        // 1. CHAT BLOQUEIO
        bool isChatOpen = (chatInstance != null && chatInstance.IsChatOpen);
        
        // 2. PAUSA BLOQUEIO 
        bool isPaused = PMMM.IsPausedLocally; 
        
        // 3. LOBBY BLOQUEIO
        bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);

        // ==========================================================
        // PRIORIDADE AO KNOCKBACK
        // ==========================================================
        if (isKnockedBack)
        {
            if (anim) anim.SetBool("Grounded", grounded);
            return;
        }

        // ==========================================================
        // BLOQUEIOS (CHAT, PAUSA, LOBBY, DEFESA, CARREGAMENTO)
        // ==========================================================
        // Se houver qualquer bloqueio, forçamos a paragem horizontal e redefinimos a animação de velocidade.
        if (lobbyBlocking || isPaused || isChatOpen || isMovementBlocked)
        {
            // O jogador é forçado a parar horizontalmente (mantendo a velocidade vertical)
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("IsSprinting", false);
            }

            // Se for Bloqueio Total (Chat/Pausa/Lobby), bloqueia TODO o input (incluindo salto e o resto do Update)
            if (lobbyBlocking || isPaused || isChatOpen)
            {
                if (anim) anim.SetBool("Grounded", grounded);
                return; // Bloqueia o resto do Update
            }
        }

        // ----------------------------------------------------------------
        // INPUT DE MOVIMENTO (Só é processado se não houver Bloqueio Total)
        // ----------------------------------------------------------------

        // RESET SALTO
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

        // INPUT HORIZONTAL (Bloqueado se isMovementBlocked=true)
        float move = 0f;
        if (!isMovementBlocked)
        {
            move = Input.GetAxisRaw("Horizontal");
            sprinting = Input.GetKey(KeyCode.LeftShift);

            float currentSpeed = sprinting ? sprintSpeed : walkSpeed;

            // APLICA MOVIMENTO
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
        }
        
        // SALTO (Bloqueado se isMovementBlocked=true)
        bool jumpInput = Input.GetKeyDown(KeyCode.W) || Input.GetButtonDown("Jump");

        if (jumpInput && jumpCount < maxJumps && !isMovementBlocked) 
        {
            // Resetar a velocidade vertical antes de aplicar a nova força de pulo para consistência
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;

            // NOVO: CHAMA O VFX DE SALTO
            SpawnJumpVFX();
        }

        // FLIP SPRITE (Permite mudar a direção (flip) mesmo que o movimento esteja bloqueado)
        if (spriteRenderer != null)
        {
            float directionInput = Input.GetAxisRaw("Horizontal"); 

            // Prioriza o flip apenas se o input de direção for dado
            if (directionInput > 0.05f) spriteRenderer.flipX = false;
            else if (directionInput < -0.05f) spriteRenderer.flipX = true;
        }

        // ANIMATOR
        if (anim)
        {
            // Se o movimento estiver bloqueado (defesa OU carregamento), Speed é 0. Caso contrário, usa o valor de move.
            anim.SetFloat("Speed", isMovementBlocked ? 0f : Mathf.Abs(move)); 
            anim.SetBool("Grounded", grounded);
            // Sprint só se não houver bloqueio E houver movimento
            bool isSprintAnim = !isMovementBlocked && sprinting && Mathf.Abs(move) > 0.05f; 
            anim.SetBool("IsSprinting", isSprintAnim);
        }
    }

    // --- Colisões (OnCollisionEnter2D, OnCollisionStay2D, OnCollisionExit2D e Gizmos inalterados) ---
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

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
