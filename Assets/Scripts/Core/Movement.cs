using UnityEngine;

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
    public bool isKnockedBack = false; // Flag para desativar o controlo

    [Header("Ataque (opcional)")]
    public Transform attackPoint; // Para sincronizar o lado do ataque com o personagem

    private Rigidbody2D rb;
    private bool sprinting;
    private bool grounded;
    private int jumpCount;

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

    // Chamado pelo Health.cs quando levas knockback
    public void SetKnockbackState(bool state)
    {
        isKnockedBack = state;
    }

    void Update()
    {
        float move = 0f; // valor de input horizontal para o Animator

        // 1) Verificar chão SEMPRE (OverlapCircle)
        if (groundCheck != null)
        {
            grounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
        }

        // Se está no chão e praticamente não está a subir/descer, reset do salto
        if (rb != null && Mathf.Abs(rb.linearVelocity.y) <= 0.1f && grounded)
        {
            jumpCount = 0;
        }

        // 2) Se estiver em knockback, não lê inputs
        if (isKnockedBack)
        {
            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("Grounded", grounded);
            }
            return;
        }

        bool isDefending = (combatSystem != null && combatSystem.isDefending);

        // 3) Se NÃO estiver a defender → movimento normal + salto
        if (!isDefending)
        {
            // Movimento horizontal
            move = Input.GetAxis("Horizontal");
            sprinting = Input.GetKey(KeyCode.LeftShift);
            float speed = sprintSpeed > 0 ? (sprinting ? sprintSpeed : walkSpeed) : walkSpeed;
            rb.linearVelocity = new Vector2(move * speed, rb.linearVelocity.y);

            // Flip do sprite conforme o lado
            if (spriteRenderer != null)
            {
                if (move > 0.05f)
                    spriteRenderer.flipX = false; // direita
                else if (move < -0.05f)
                    spriteRenderer.flipX = true;  // esquerda
            }

            // Manter o ponto de ataque do lado certo
            if (attackPoint != null && spriteRenderer != null)
            {
                float attackX = Mathf.Abs(attackPoint.localPosition.x);
                attackPoint.localPosition = new Vector3(
                    spriteRenderer.flipX ? -attackX : attackX,
                    attackPoint.localPosition.y,
                    attackPoint.localPosition.z
                );
            }

            // Salto com W (duplo salto)
            if (Input.GetKeyDown(KeyCode.W))
            {
                // 🔒 FAIL-SAFE: se estivermos praticamente parados em Y,
                // começamos uma nova sequência de saltos
                if (rb != null && Mathf.Abs(rb.linearVelocity.y) <= 0.1f)
                {
                    jumpCount = 0;
                }

                if (jumpCount < maxJumps)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                    jumpCount++;
                }
            }
        }
        else
        {
            // 4) A defender → não anda nem salta, mas a gravidade continua
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            move = 0f;
        }

        // 5) Atualizar Animator
        if (anim)
        {
            anim.SetFloat("Speed", Mathf.Abs(move));
            anim.SetBool("Grounded", grounded);
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

    // Redundância de chão via colisão (mantida)
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
            grounded = false;
        }
    }
}
