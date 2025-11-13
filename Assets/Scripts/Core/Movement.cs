using UnityEngine;
using Photon.Pun;

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

    // --- NOVA PROPRIEDADE PARA WALL CHECK ---
    [Header("Wall Check")]
    private bool isTouchingWall = false; // Flag para saber se está a tocar numa parede lateralmente

    [Header("Knockback")]
    public bool isKnockedBack = false; // Flag para desativar o controle

    // --- PROPRIEDADES DE LEITURA PARA SINCRONIZAÇÃO (PlayerSetup.cs) ---
    public float CurrentHorizontalSpeed => rb.linearVelocity.x;
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

    void Update()
    {
        // 1. VERIFICAR CHÃO SEMPRE (OverlapCircle)
        if (groundCheck != null)
        {
            grounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
        }

        // --- LÓGICA DE KNOCKBACK ---
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

        // 2. LÓGICA DE RESET DE SALTO (MODIFICADA)
        if (rb != null)
        {
            // Reset principal se estiver no chão
            if (grounded && Mathf.Abs(rb.linearVelocity.y) <= 0.1f)
            {
                jumpCount = 0;
                isTouchingWall = false; // Garante que a flag de parede é limpa
            }
            // Reset se estiver a tocar na parede e não estiver no chão (para permitir Wall Jump)
            else if (isTouchingWall && !grounded)
            {
                jumpCount = 0; // <--- RESET DO SALTO NA PAREDE
            }
        }

        float move = 0f;
        bool isDefending = (combatSystem != null && combatSystem.isDefending);

        // 3. LÓGICA DE MOVIMENTO E SALTO (SÓ se NÃO estiver a defender)
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
                // Se não houver input, define a velocidade horizontal para zero para parar, mas só se não estiver a tocar na parede (para evitar slide indesejado)
                if (!isTouchingWall || grounded)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }

            // Salto com W (duplo salto)
            if (Input.GetKeyDown(KeyCode.W) && jumpCount < maxJumps)
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
        else
        {
            // 4. A defender → Para o movimento horizontal
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            move = 0f;
        }

        // 5. Atualizar Animator
        if (anim)
        {
            anim.SetFloat("Speed", isDefending ? 0f : Mathf.Abs(move));
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

    // --- Lógica de Colisão (MODIFICADA) ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            // O ideal é iterar sobre todos os contactos, mas para simplificar:
            ContactPoint2D contact = collision.GetContact(0);

            // A. Colisão por baixo (Chão)
            if (contact.normal.y > 0.5f)
            {
                grounded = true;
                jumpCount = 0;
                isTouchingWall = false; // Não é parede se for chão
            }
            // B. Colisão Lateral (Parede)
            else if (Mathf.Abs(contact.normal.x) > 0.5f && !grounded)
            {
                // Verifica se a colisão é lateral e não estás no chão (para priorizar o ground check)
                isTouchingWall = true;
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Usamos o OnCollisionStay2D para manter a flag 'isTouchingWall' ativa enquanto estamos encostados.
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
            // Quando sai da colisão com o objeto da groundLayer, desativa a flag de parede.
            isTouchingWall = false;
        }
    }
}