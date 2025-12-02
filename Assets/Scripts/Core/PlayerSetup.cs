using Photon.Pun;
using TMPro;
using UnityEngine;

// Implementa IPunObservable para sincronização de dados de animação
public class PlayerSetup : MonoBehaviourPunCallbacks, IPunObservable
{
    public Movement2D movement;
    public GameObject camara;
    public CombatSystem2D combat;
    public string nickname;
    public TextMeshPro nicknameText; // Usando TextMeshPro para o Nickname flutuante
    
    // --- NOVO: Referência ao Sprite Renderer ---
    private SpriteRenderer spriteRenderer; 

    // Referências de sincronização
    private PhotonView photonView;
    private Animator anim;

    // Variáveis usadas para interpolar animação em clientes remotos
    private float syncSpeed;
    private bool syncGrounded;
    // --- NOVO: Variáveis de Sincronização ---
    private bool syncFlipX; 
    private bool syncIsDefending;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        anim = GetComponent<Animator>();
        // Obtém a referência do SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>(); 

        // Apenas o cliente local deve processar o input, ativar a câmara, etc.
        if (photonView.IsMine)
        {
            IsLocalPlayer();
        }
        else // Cliente Remoto
        {
            // Desativa o controle e o combate nos clientes remotos
            if (movement != null) movement.enabled = false;
            if (combat != null) combat.enabled = false;

            // Certifique-se de que a câmara não está ativa em jogadores remotos
            if (camara != null) camara.SetActive(false);
        }
    }

    // Chamado pelo Room Manager para configurar o jogador local
    public void IsLocalPlayer()
    {
        // Ativa os scripts de input
        if (movement != null) movement.enabled = true;

        // Ativa a câmara
        if (camara != null)
        {
            camara.SetActive(true);

            // Inicia o zoom dinâmico quando a câmara for ativada
            var zoomDynamic = camara.GetComponent<CameraDynamicZoom>();
            if (zoomDynamic != null)
                zoomDynamic.enabled = true;
        }

        // Enable combat system for the local player only
        if (combat != null)
            combat.enabled = true;
    }

    // --- SINCRONIZAÇÃO DE ANIMAÇÃO E ESTADO PARA JOGADORES REMOTOS ---

    void Update()
    {
        // Se não for o seu objeto, aplica as animações sincronizadas
        if (!photonView.IsMine)
        {
            if (anim)
            {
                // Aplica os valores sincronizados recebidos no OnPhotonSerializeView
                // 1. Sincroniza o parâmetro de Corrida
                anim.SetFloat("Speed", syncSpeed); 
                
                // 2. Sincroniza o parâmetro de Chão
                anim.SetBool("Grounded", syncGrounded);
                
                // 3. NOVO: Sincroniza o parâmetro de Defesa
                anim.SetBool("IsDefending", syncIsDefending);

                // Nota: O parâmetro "IsSprinting" é tipicamente ligado à velocidade, 
                // mas pode ser sincronizado se a animação for distinta (por agora, usamos a velocidade syncSpeed).

                // 4. NOVO: Sincroniza o Flip do Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = syncFlipX;
                }
            }
        }
        else
        {
            // NOVO: Garantir que o Animator do jogador local está sempre a par do estado de Defesa
            if (combat != null && anim)
            {
                 // Nota: A lógica de Attack/Defense no CombatSystem2D já deve usar o RPC (SetDefenseState)
                 // que corre em todos os clientes. No entanto, o OnPhotonSerializeView ainda é útil
                 // para garantir que novos jogadores que entram na sala vejam o estado correto.
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // --- O JOGADOR LOCAL ESTÁ A ENVIAR DADOS ---

            // 1. Velocidade Horizontal (para animação de corrida)
            stream.SendNext(movement.CurrentHorizontalSpeed);
            
            // 2. Estado no Chão
            stream.SendNext(movement.IsGrounded);
            
            // 3. NOVO: Estado de Defesa
            stream.SendNext(combat != null && combat.isDefending);

            // 4. NOVO: Flip do Sprite
            if (spriteRenderer != null)
            {
                 stream.SendNext(spriteRenderer.flipX);
            }
        }
        else
        {
            // --- O JOGADOR REMOTO ESTÁ A RECEBER DADOS ---

            // 1. Velocidade Horizontal
            this.syncSpeed = (float)stream.ReceiveNext();
            
            // 2. Estado no Chão
            this.syncGrounded = (bool)stream.ReceiveNext();
            
            // 3. NOVO: Estado de Defesa
            this.syncIsDefending = (bool)stream.ReceiveNext();
            
            // 4. NOVO: Flip do Sprite
            if (spriteRenderer != null)
            {
                 this.syncFlipX = (bool)stream.ReceiveNext();
            }
        }
    }

    [PunRPC]
    public void SetNickname(string _nickname)
    {
        nickname = _nickname;
        
        // Atribui o nickname ao TextMeshPro flutuante
        if (nicknameText != null)
        {
            nicknameText.text = nickname;
        }
    }
}
