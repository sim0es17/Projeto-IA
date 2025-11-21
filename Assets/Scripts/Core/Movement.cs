using UnityEngine;
using Photon.Pun;
using System.Collections; // Necessário para as Coroutines

[RequireComponent(typeof(Rigidbody2D))]
public class Movement2D : MonoBehaviour
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
    private bool isTouchingWall = false; // Flag para saber se está a tocar numa parede lateralmente

    [Header("Knockback")]
    public bool isKnockedBack = false; // Flag para desativar o controle

    // --- VARIÁVEIS INTERNAS DO POWER UP ---
    private float defaultWalkSpeed;
    private float defaultSprintSpeed;
    private float defaultJumpForce;
    private Coroutine currentBuffRoutine;
    // -------------------------------------

    // --- PROPRIEDADES DE LEITURA PARA SINCRONIZAÇÃO (PlayerSetup.cs) ---
    // Nota: Se estiveres numa versão anterior ao Unity 6, usa rb.velocity em vez de rb.linearVelocity
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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        combatSystem = GetComponent<CombatSystem2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        pv = GetComponent<PhotonView>();

        // --- 1. GUARDAR OS VALORES ORIGINAIS NO INÍCIO ---
        defaultWalkSpeed = walkSpeed;
        defaultSprintSpeed = sprintSpeed;
        defaultJumpForce = jumpForce;
        // -------------------------------------------------

        if (groundCheck == null)
            Debug.LogWarning("GroundCheck não atribuído no inspector! Adicione um objeto filho para o check.");

        // Se este NÃO for o jogador local, desativa o script para prevenir inputs.
        if (pv == null || !pv.IsMine)
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

    // --- MÉTODOS DO POWER UP ---
    public void ActivateSpeedJumpBuff(float duration, float speedMultiplier, float jumpMultiplier)
    {
        // Se já existir um buff a correr, paramos para reiniciar/atualizar
        if (currentBuffRoutine != null)
        {
            StopCoroutine(currentBuffRoutine);
            ResetStats(); // Garante que partimos dos valores base antes de multiplicar de novo
        }

        currentBuffRoutine = StartCoroutine(BuffRoutine(duration, speedMultiplier, jumpMultiplier));
    }

    private IEnumerator BuffRoutine(float duration, float speedMult, float jumpMult)
    {
        // Aplica os multiplicadores aos valores base
        walkSpeed = defaultWalkSpeed * speedMult;
        sprintSpeed = defaultSprintSpeed * speedMult;
        jumpForce = defaultJumpForce * jumpMult;

        // Opcional: Aqui podias mudar a cor do player para indicar o buff
        // spriteRenderer.color = Color.yellow; 

        yield return new WaitForSeconds(duration);

        // O tempo acabou, resetar stats
        ResetStats();
        currentBuffRoutine = null;
    }

    private void ResetStats()
    {
        walkSpeed = defaultWalkSpeed;
        sprintSpeed = defaultSprintSpeed;
        jumpForce = defaultJumpForce;

        // Resetar cor se tivesses mudado
        // spriteRenderer.color = Color.white;
    }
    // ---------------------------

    void Update()
    {
        // VERIFICAR CHÃO SEMPRE (OverlapCircle)
        if (groundCheck != null)
        {
            grounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
        }

        // LÓGICA DE KNOCKBACK
        if (isKnockedBack)
        {
            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("Grounded", grounded);
            }
            return; // IGNORA O RESTO DA LÓGICA DE INPUT
        }

        // Se chegámos aqui, o controlo está ATIVO.

        // LÓGICA DE RESET DE SALTO
        if (rb != null)
        {
            // Reset principal se estiver no chão
            if (grounded && Mathf.Abs(rb.linearVelocity.y) <= 0.1f)
            {
                jumpCount = 0;
                isTouchingWall = false; 
            }
            // Reset se estiver a tocar na parede e não estiver no chão (para permitir Wall Jump)
            else if (isTouchingWall && !grounded)
            {
                jumpCount = 0; 
            }
        }

        float move = 0f;
        bool isDefending = (combatSystem != null && combatSystem.isDefending);

        // LÓGICA DE MOVIMENTO E SALTO (SÓ se NÃO estiver a defender)
        if (!isDefending)
        {
            // Movimento horizontal
            move = Input.GetAxisRaw("Horizontal");
            sprinting = Input.GetKey(KeyCode.LeftShift);

            // Usa a velocidade atual (que pode estar alterada pelo PowerUp)
            float currentSpeed = sprinting ? sprintSpeed : walkSpeed;

            // Aplica a velocidade de movimento
            if (Mathf.Abs(move) > 0.05f)
            {
                rb.linearVelocity = new Vector2(move * currentSpeed, rb.linearVelocity.y);
            }
            else
            {
                // Se não houver input e não for wall slide, parar
                if (!isTouchingWall || grounded)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }

            // Salto com W (duplo salto)
            if (Input.GetKeyDown(KeyCode.W) && jumpCount < maxJumps)
            {
                // Usa a força de salto atual (que pode estar alterada pelo PowerUp)
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
        else
        {
            // A defender -> Para o movimento horizontal
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            move = 0f;
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

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    // --- Lógica de Colisão ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            ContactPoint2D contact = collision.GetContact(0);

            // A. Colisão por baixo (Chão)
            if (contact.normal.y > 0.5f)
            {
                grounded = true;
                jumpCount = 0;
                isTouchingWall = false;
            }
            // B. Colisão Lateral (Parede)
            else if (Mathf.Abs(contact.normal.x) > 0.5f && !grounded)
            {
                isTouchingWall = true;
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            ContactPoint2D contact = collision.GetContact(0);

            // Só ativa se for lateral e não for chão
            if (Mathf.Abs(contact.normal.x) > 0.5f && contact.normal.y < 0.5f)
            {
                isTouchingWall = true;
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
}
