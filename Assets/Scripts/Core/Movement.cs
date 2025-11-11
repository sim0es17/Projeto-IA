using UnityEngine;
using Photon.Pun;

// Garante que o script PlayerSetup (ou um script que chame IsLocalPlayer)
// está no objeto para obter o PhotonView se necessário para controle local.

[RequireComponent(typeof(Rigidbody2D))]
public class Movement2D : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 5f; // Valores padrão para segurança
    public float sprintSpeed = 8f;

    [Header("Pulo")]
    public float jumpForce = 10f;
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

    // Opcional: Assumindo que CombatSystem2D existe e tem a flag isDefending
    private CombatSystem2D combatSystem;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    // Referência ao PhotonView para saber se este é o jogador local.
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

        // No início, forçamos o isKnockedBack para false.
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

        // --- 0. VERIFICAÇÃO DO LOBBY & KNOCKBACK ---
        // Se o jogo não começou OU estiver em Knockback, bloqueia todo o controlo de input.
        if (!LobbyManager.GameStartedAndPlayerCanMove || isKnockedBack)
        {
            // Parar completamente o movimento horizontal
            if (rb != null)
            {
                // Zera a velocidade horizontal, mas mantém a vertical (para cair ou ser empurrado)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            if (anim)
            {
                anim.SetFloat("Speed", 0f);
                anim.SetBool("Grounded", grounded);
            }
            return; // IGNORA O RESTO DA LÓGICA DE INPUT
        }


        // Se chegámos aqui, o controlo está ATIVO (jogo começou e não está em Knockback).

        // 2. LÓGICA DE RESET DE SALTO
        // Se está no chão E está quase parado verticalmente, reseta o salto
        if (rb != null && grounded && Mathf.Abs(rb.linearVelocity.y) <= 0.1f)
        {
            jumpCount = 0;
        }

        float move = 0f;
        bool isDefending = (combatSystem != null && combatSystem.isDefending);

        // 3. LÓGICA DE MOVIMENTO E SALTO (SÓ se NÃO estiver a defender)
        if (!isDefending)
        {
            // Movimento horizontal
            move = Input.GetAxisRaw("Horizontal"); // Usar GetAxisRaw para movimento instantâneo
            sprinting = Input.GetKey(KeyCode.LeftShift);

            // Lógica de velocidade
            // Certifique-se de que walkSpeed e sprintSpeed são > 0 no Inspector
            float currentSpeed = sprinting ? sprintSpeed : walkSpeed;

            rb.linearVelocity = new Vector2(move * currentSpeed, rb.linearVelocity.y);

            // Salto com W (duplo salto)
            if (Input.GetKeyDown(KeyCode.W) && jumpCount < maxJumps)
            {
                // Reseta a velocidade vertical para garantir um salto consistente
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
            // 4. A defender → Para o movimento horizontal
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            move = 0f; // Para que a animação "Speed" seja zero
        }

        // 5. Atualizar Animator
        if (anim)
        {
            // Se estiver a defender, o Speed deve ser 0 para animação de defesa estática
            anim.SetFloat("Speed", isDefending ? 0f : Mathf.Abs(move));
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

    // --- Lógica de Colisão (Melhorada para evitar problemas de GroundCheck) ---

    // Redundância de chão via colisão (Corrige o uso de linearVelocity para velocity)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Verifica se a camada colide com o 'groundLayer'
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
            // Se o jogador estiver a cair (velocidade vertical negativa), considera que saiu do chão.
            if (rb != null && rb.linearVelocity.y < 0)
            {
                grounded = false;
            }
        }
    }
}