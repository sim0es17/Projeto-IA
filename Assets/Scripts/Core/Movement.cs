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
    private CombatSystem2D combatSystem;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private PhotonView pv;

    private GameChat chatInstance;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        combatSystem = GetComponent<CombatSystem2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        pv = GetComponent<PhotonView>();

        chatInstance = FindObjectOfType<GameChat>();

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

    // ----------------------------------------------------
    // --- MÉTODOS DO POWER UP (MULTIPLAYER - RPC) ---
    // ----------------------------------------------------

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
        // Soma à velocidade base
        walkSpeed += boostAmount;
        sprintSpeed += boostAmount;

        Debug.Log($"[Multiplayer] Velocidade aumentada!");

        yield return new WaitForSeconds(duration);

        ResetStats();
        currentBuffRoutine = null;
    }

    // ----------------------------------------------------
    // --- MÉTODOS DO POWER UP (SINGLE PLAYER - LEGACY) ---
    // ----------------------------------------------------
    // Adicionei isto de volta para o teu erro desaparecer!

    public void ActivateSpeedJumpBuff(float speedMultiplier, float jumpMultiplier, float duration)
    {
        // Garante que só o dono local ativa
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
        // Aplica multiplicadores (Lógica antiga)
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

    // ----------------------------------------------------

    void Update()
    {
        if (pv != null && !pv.IsMine) return;

        // Ground Check
        if (groundCheck != null)
        {
            grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        bool isDefending = (combatSystem != null && combatSystem.isDefending);
        bool isChatOpen = (chatInstance != null && chatInstance.IsChatOpen);
        bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);
        bool isPaused = false;

        // PRIORIDADE AO KNOCKBACK
        if (isKnockedBack)
        {
            if (anim) anim.SetBool("Grounded", grounded);
            return;
        }

        // BLOQUEIOS
        if (lobbyBlocking || isPaused || isChatOpen || isDefending)
        {
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("IsSprinting", false);
            }

            if (lobbyBlocking || isPaused || isChatOpen)
            {
                if (anim) anim.SetBool("Grounded", grounded);
                return;
            }
        }

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

        float move = Input.GetAxisRaw("Horizontal");
        sprinting = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = sprinting ? sprintSpeed : walkSpeed;

        // APLICA MOVIMENTO
        if (Mathf.Abs(move) > 0.05f)
        {
            rb.linearVelocity = new Vector2(move * currentSpeed, rb.linearVelocity.y);
        }
        else
        {
            if (!isTouchingWall || grounded)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        // SALTO
        bool jumpInput = Input.GetKeyDown(KeyCode.W) || Input.GetButtonDown("Jump");

        if (jumpInput && jumpCount < maxJumps)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
        }

        // FLIP SPRITE
        if (spriteRenderer != null)
        {
            if (move > 0.05f) spriteRenderer.flipX = false;
            else if (move < -0.05f) spriteRenderer.flipX = true;
        }

        // ANIMATOR
        if (anim)
        {
            anim.SetFloat("Speed", isDefending ? 0f : Mathf.Abs(move));
            anim.SetBool("Grounded", grounded);
            bool isSprintAnim = !isDefending && sprinting && Mathf.Abs(move) > 0.05f;
            anim.SetBool("IsSprinting", isSprintAnim);
        }
    }

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