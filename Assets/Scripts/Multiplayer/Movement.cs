using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Movement2D : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed;
    public float sprintSpeed;

    [Header("Pulo")]
    public float jumpForce;
    public int maxJumps;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private bool sprinting;
    private bool grounded;
    private int jumpCount;


    private CombatSystem2D combatSystem;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        combatSystem = GetComponent<CombatSystem2D>();

        if (groundCheck == null)
            Debug.LogWarning("GroundCheck não atribuído no inspector!");
    }

    void Update()
    {
        // Se estiver a defender, para o movimento horizontal e ignora o resto da função.
        if (combatSystem != null && combatSystem.isDefending)
        {
            // Força a paragem horizontal (mas deixa a gravidade atuar)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return; // Impede a leitura de inputs de movimento e pulo
        }


        // Movimento horizontal
        float move = Input.GetAxis("Horizontal");
        sprinting = Input.GetKey(KeyCode.LeftShift);
        float speed = sprinting ? sprintSpeed : walkSpeed;
        rb.linearVelocity = new Vector2(move * speed, rb.linearVelocity.y);

        // Checar chão com raycast
        grounded = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, groundLayer);

        // Reset de jumps ao tocar no chão
        if (grounded)
        {
            jumpCount = 0;
        }

        // Salto com W
        if (Input.GetKeyDown(KeyCode.W) && jumpCount < maxJumps)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);
            Gizmos.DrawWireSphere(groundCheck.position + Vector3.down * groundCheckDistance, 0.05f);
        }
    }

    // Redundância com colisão para detectar chão com segurança
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
