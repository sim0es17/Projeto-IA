using UnityEngine;
using Photon.Pun;
using System.Collections; 
using System.Linq; 

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

    // Propriedades de Acesso
    public float CurrentHorizontalSpeed => rb != null ? rb.linearVelocity.x : 0f;
    public bool IsGrounded => grounded;

    // --- Referências de Componentes e Singletons ---
    private Rigidbody2D rb;
    private bool sprinting;
    private bool grounded;
    private int jumpCount;
    private CombatSystem2D combatSystem; // Assumindo que existe
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private PhotonView pv; 
    
    // --- REFERÊNCIA DO SINGLETON DO CHAT (É MELHOR OBTER A INSTÂNCIA AQUI) ---
    private GameChat chatInstance; // Assumindo que existe
    // Assumindo que PMMM é uma classe estática de gestão de pausa
    // private PMMM pmmmInstance; 


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // É vital garantir que estes componentes existem
        combatSystem = GetComponent<CombatSystem2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        pv = GetComponent<PhotonView>();
        
        // ** CORREÇÃO 1: OBTEM A INSTÂNCIA DO CHAT **
        // Se a sua classe GameChat for um Singleton
        // chatInstance = GameChat.instance; 

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
    // CORREÇÃO CRÍTICA AQUI: A ordem dos argumentos foi ajustada para:
    // (speedMultiplier, jumpMultiplier, duration)
    public void ActivateSpeedJumpBuff(float speedMultiplier, float jumpMultiplier, float duration)
    {
        // Garante que só o dono local ativa o buff
        if (pv == null || !pv.IsMine) return; 
        
        // Se já houver um buff ativo, para o anterior e reseta os stats.
        if (currentBuffRoutine != null)
        {
            StopCoroutine(currentBuffRoutine);
            ResetStats(); // Garante que partimos dos valores base antes de multiplicar de novo
        }

        // Inicia a nova corrotina com os novos valores
        currentBuffRoutine = StartCoroutine(BuffRoutine(duration, speedMultiplier, jumpMultiplier));

        // Opcional: Avisar outros jogadores (se quiserem ver um efeito visual)
        // pv.RPC("RPC_ShowBuffEffect", RpcTarget.Others, duration);
    }

    private IEnumerator BuffRoutine(float duration, float speedMult, float jumpMult)
    {
        // Aplica os multiplicadores aos valores base
        walkSpeed = defaultWalkSpeed * speedMult;
        sprintSpeed = defaultSprintSpeed * speedMult;
        jumpForce = defaultJumpForce * jumpMult;

        // Opcional: Efeito visual/sonoro
        // spriteRenderer.color = Color.yellow; 

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

        // Resetar cor/efeitos
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

        // Verifica o bloqueio principal: Knockback, Pausa, ou Chat
        // isDefending é uma verificação adicional
        bool isDefending = (combatSystem != null && combatSystem.isDefending);
        bool isChatOpen = (chatInstance != null && chatInstance.IsChatOpen);
        // bool isPaused = PMMM.IsPausedLocally; // Assumindo a classe PMMM existe

        // ** LÓGICA DE BLOQUEIO DO CONTROLE **
        if (isKnockedBack /* || isPaused */ || isChatOpen || isDefending)
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

            // Apenas o Knockback e a defesa permitem que a gravidade continue. 
            // Pausa e Chat devem parar o movimento, mas o 'return' não impede a gravidade de atuar.
            if (isKnockedBack || isDefending)
            {
                // Permite que o movimento de knockback ou a queda devido à defesa continue, mas ignora o input.
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
            // Reset principal se estiver no chão e a velocidade vertical for baixa
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
        // Se estiver a defender, o movimento é bloqueado (já tratado no topo do Update)

        // Atualizar Animator
        if (anim)
        {
            anim.SetFloat("Speed", isDefending ? 0f : Mathf.Abs(move));
            anim.SetBool("Grounded", grounded);
            bool isSprintAnim = !isDefending && sprinting && Mathf.Abs(move) > 0.05f;
            anim.SetBool("IsSprinting", isSprintAnim);
        }
    }

    // A lógica de colisão para Ground e Wall Check
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            if (collision.contactCount > 0)
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
            // Pode sair da parede
            isTouchingWall = false;
            // A verificação principal de grounded é feita no Update via OverlapCircle.
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
