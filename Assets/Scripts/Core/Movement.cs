using UnityEngine;
using Photon.Pun;
using System.Collections; 
using System.Linq; // Necessário para a lógica de colisão no OnCollisionEnter2D

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
    private bool isTouchingWall = false; // Flag para saber se está a tocar numa parede lateralmente

    [Header("Knockback")]
    public bool isKnockedBack = false; // Flag para desativar o controle

    // --- VARIÁVEIS INTERNAS DO POWER UP ---
    private float defaultWalkSpeed;
    private float defaultSprintSpeed;
    private float defaultJumpForce;
    private Coroutine currentBuffRoutine;
    // -------------------------------------

    //  AJUSTE: Mudar para rb.velocity para compatibilidade 
    public float CurrentHorizontalSpeed => rb != null ? rb.linearVelocity.x : 0f;
    public bool IsGrounded => grounded;

    private Rigidbody2D rb;
    private bool sprinting;
    private bool grounded;
    private int jumpCount;

    //  ASSUME-SE A EXISTÊNCIA DE CombatSystem2D, PMMM e GameChat.cs 
    private CombatSystem2D combatSystem;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    //  PhotonView é obtido no Start() 
    private PhotonView pv; 
    
    //  Variáveis estáticas de Lock assumidas para integração 
    // Se PMMM.cs existir:
    private bool IsPausedLocally => PMMM.IsPausedLocally; 
    // Se GameChat.cs existir:
    private bool IsChatOpen => GameChat.IsChatOpen;


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
        if (pv == null || !pv.IsMine) return; // Garante que só o local ativa o buff
        
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

        //  LÓGICA DE BLOQUEIO GERAL: Knockback, Pausa ou Chat 
        if (isKnockedBack || IsPausedLocally || IsChatOpen)
        {
            // Se estiver bloqueado (exceto por Knockback), garantir que o movimento para.
            if (rb != null && !isKnockedBack)
            {
                //  AJUSTE: Usar rb.velocity e 0f no eixo X 
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            
            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("Grounded", grounded);
                anim.SetBool("IsSprinting", false);
            }
            return; // IGNORA O RESTO DA LÓGICA DE INPUT
        }

        // Se chegámos aqui, o controlo está ATIVO.

        // LÓGICA DE RESET DE SALTO (Mantida a lógica original, mas com ajuste de velocidade)
        if (rb != null)
        {
            // Reset principal se estiver no chão
            if (grounded && Mathf.Abs(rb.linearVelocity.y) <= 0.1f) //  AJUSTE: rb.velocity.y 
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
        // Assume-se que o CombatSystem2D tem uma propriedade pública isDefending
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
                //  AJUSTE: Usar rb.velocity 
                rb.linearVelocity = new Vector2(move * currentSpeed, rb.linearVelocity.y);
            }
            else
            {
                // Se não houver input e não for wall slide, parar
                if (!isTouchingWall || grounded)
                {
                    //  AJUSTE: Usar rb.velocity 
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }

            // Salto com W (duplo salto)
            //  AJUSTE: Adicionar Input.GetButtonDown("Jump") para barra de espaço 
            bool jumpInput = Input.GetKeyDown(KeyCode.W) || Input.GetButtonDown("Jump");

            if (jumpInput && jumpCount < maxJumps)
            {
                // Usa a força de salto atual (que pode estar alterada pelo PowerUp)
                //  AJUSTE: Usar rb.velocity 
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
            //  AJUSTE: Usar rb.velocity 
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

    //  AJUSTE: Correção da lógica de Wall Check na colisão 

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            // Se houver mais do que 0 contactos (deve ser sempre verdade em Enter)
            if (collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
    
                // A. Colisão por baixo (Chão) - Limiar de 0.5f para ser flexível
                if (contact.normal.y > 0.5f)
                {
                    grounded = true;
                    jumpCount = 0;
                    isTouchingWall = false;
                }
                // B. Colisão Lateral (Parede) - Limiar de 0.5f para ser parede, e não chão
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
    
                // Atualiza o estado de toque no chão
                if (contact.normal.y > 0.5f)
                {
                    grounded = true;
                    isTouchingWall = false;
                }
                
                // Atualiza o estado de toque na parede (lateral e não chão)
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
            // Se o chão for perdido, grounded deve ser falso
            // Note que grounded também é verificado no Update via OverlapCircle, o que é mais robusto.
            // Para evitar conflitos, a principal responsabilidade do grounded fica no Update.
        }
    }
    
    // -------------------------------------------------------------
    // Gizmos (Inalterado)
    // -------------------------------------------------------------
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
