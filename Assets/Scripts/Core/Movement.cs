using UnityEngine;
using Photon.Pun; // Mantido para contexto

[RequireComponent(typeof(Rigidbody2D))]
public class Movement2D : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed;
    public float sprintSpeed;

    [Header("Pulo")]
    public float jumpForce;
    public int maxJumps = 2;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    [Header("Knockback")]
    public bool isKnockedBack = false; // Flag para desativar o controle

    // --- PROPRIEDADES DE LEITURA PARA SINCRONIZAÇÃO (PlayerSetup.cs) ---
    public float CurrentHorizontalSpeed => rb.linearVelocity.x;
    public bool IsGrounded => grounded;

    private Rigidbody2D rb;
    private bool sprinting;
    private bool grounded;
    private int jumpCount;
    // Opcional: Mantido para a verificação de defesa, mas pode ser removido
    private CombatSystem2D combatSystem;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        combatSystem = GetComponent<CombatSystem2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (groundCheck == null)
            Debug.LogWarning("GroundCheck não atribuído no inspector!");
    }

    // Método público para ser chamado pelo Health.cs
    public void SetKnockbackState(bool state)
    {
        isKnockedBack = state;
    }

    void Update()
    {
        // 1. VERIFICAR CHÃO SEMPRE (OverlapCircle, mais robusto)
        if (groundCheck != null)
        {
            grounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
        }

        // 2. LÓGICA DE RESET DE SALTO
        // Se está no chão E está quase parado verticalmente, reseta o salto
        if (rb != null && grounded && Mathf.Abs(rb.linearVelocity.y) <= 0.1f)
        {
            jumpCount = 0;
        }

        float move = 0f;

        // 3. ESTADO DE KNOCKBACK
        if (isKnockedBack)
        {
            // Atualiza animações de queda/pouso, mas sem movimento
            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("Grounded", grounded);
            }
            return;
        }

        bool isDefending = (combatSystem != null && combatSystem.isDefending);

        // 4. LÓGICA DE MOVIMENTO E SALTO (SÓ se NÃO estiver a defender)
        if (!isDefending)
        {
            // Movimento horizontal
            move = Input.GetAxis("Horizontal");
            sprinting = Input.GetKey(KeyCode.LeftShift);

            // Lógica de velocidade
            float currentSpeed = sprintSpeed > 0 ? (sprinting ? sprintSpeed : walkSpeed) : walkSpeed;

            rb.linearVelocity = new Vector2(move * currentSpeed, rb.linearVelocity.y);

            // Salto com W (duplo salto)
            if (Input.GetKeyDown(KeyCode.W) && jumpCount < maxJumps)
            {
                // Reseta a velocidade vertical antes do salto para consistência
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpCount++;
            }

            // Flip do sprite conforme o lado
            if (spriteRenderer != null)
            {
                if (move > 0.05f)
                    spriteRenderer.flipX = false; // direita
                else if (move < -0.05f)
                    spriteRenderer.flipX = true;  // esquerda
            }
        }
        else
        {
            // 5. A defender → Para o movimento horizontal
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            move = 0f; // Para que a animação "Speed" seja zero
        }

        // 6. Atualizar Animator
        if (anim)
        {
            anim.SetFloat("Speed", Mathf.Abs(move));
            anim.SetBool("Grounded", grounded);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Usa o Gizmos do OverlapCircle para visualizar
        if (groundCheck != null)
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    // Redundância de chão via colisão
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            grounded = true;
            jumpCount = 0;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            // Só desativa 'grounded' se a velocidade vertical for negativa (a cair)
            if (rb != null && rb.linearVelocity.y < 0)
            {
                grounded = false;
            }
        }
    }
}
