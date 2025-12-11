using Photon.Pun;
using TMPro;
using UnityEngine;

// Implementa IPunObservable para sincronização de dados de animação
public class PlayerSetup : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Componentes Obrigatórios")]
    public Movement2D movement;
    public GameObject camara; // ARRASTA A CÂMARA DO BONECO PARA AQUI NO PREFAB
    public CombatSystem2D combat;

    [Header("UI")]
    // Este é o TextMeshPro que deve estar anexado ACIMA DA CABEÇA do prefab do jogador
    public TextMeshPro nicknameText;

    // Referências Privadas
    private SpriteRenderer spriteRenderer;
    private PhotonView photonView;
    private Animator anim;
    private string currentNickname = "Player"; // Novo campo privado para guardar o nome

    // Variáveis de Sincronização
    private float syncSpeed;
    private bool syncGrounded;
    private bool syncFlipX;
    private bool syncIsDefending;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // Se for o meu boneco (o que eu controlo)...
        if (photonView.IsMine)
        {
            IsLocalPlayer();
            
            // =========================================================================
            // PASSO CHAVE: Envia o nome de rede (que vem do RoomManager) para todos
            // RpcTarget.AllBuffered garante que quem entrar depois também receba o nome.
            // =========================================================================
            currentNickname = PhotonNetwork.LocalPlayer.NickName;
            
            // Certifique-se de que o nome não é nulo/vazio antes de enviar o RPC
            if (string.IsNullOrEmpty(currentNickname))
            {
                currentNickname = "Jogador_" + photonView.OwnerActorNr;
            }
            
            photonView.RPC("SetNickname", RpcTarget.AllBuffered, currentNickname);
        }
        else // Se for boneco de outro jogador...
        {
            DisableRemotePlayer();
            // Para jogadores remotos, o nome será definido quando o RPC for recebido.
        }
    }

    // Chamado para configurar o JOGADOR LOCAL (EU)
    public void IsLocalPlayer()
    {
        Debug.Log($"[PlayerSetup] Configurando jogador local: {gameObject.name}");

        if (movement != null) movement.enabled = true;
        if (combat != null) combat.enabled = true;

        // ATIVAÇÃO DA CÂMARA
        if (camara != null)
        {
            camara.SetActive(true);
            AudioListener listener = camara.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
            var zoomDynamic = camara.GetComponent<CameraDynamicZoom>();
            if (zoomDynamic != null) zoomDynamic.enabled = true;
        }
    }

    // Chamado para configurar JOGADORES REMOTOS (OUTROS)
    public void DisableRemotePlayer()
    {
        if (movement != null) movement.enabled = false;
        if (combat != null) combat.enabled = false;

        // Garante que a câmara dos outros está DESLIGADA
        if (camara != null)
        {
            camara.SetActive(false);
            AudioListener listener = camara.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    // --- SINCRONIZAÇÃO DE ANIMAÇÕES E DADOS (CORRETO) ---

    void Update()
    {
        if (!photonView.IsMine)
        {
            // Lógica de interpolação e sincronização para jogadores remotos
            if (anim)
            {
                anim.SetFloat("Speed", syncSpeed);
                anim.SetBool("Grounded", syncGrounded);
                anim.SetBool("IsDefending", syncIsDefending);

                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = syncFlipX;
                }
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // ENVIA DADOS
            stream.SendNext(movement.CurrentHorizontalSpeed);
            stream.SendNext(movement.IsGrounded);
            stream.SendNext(combat != null && combat.isDefending);

            if (spriteRenderer != null) stream.SendNext(spriteRenderer.flipX);
        }
        else
        {
            // RECEBE DADOS
            this.syncSpeed = (float)stream.ReceiveNext();
            this.syncGrounded = (bool)stream.ReceiveNext();
            this.syncIsDefending = (bool)stream.ReceiveNext();

            if (spriteRenderer != null) this.syncFlipX = (bool)stream.ReceiveNext();
        }
    }

    // =========================================================================
    // RPC: Recebe o nome de todos os clientes e define o texto acima da cabeça
    // =========================================================================
    [PunRPC]
    public void SetNickname(string _nickname)
    {
        currentNickname = _nickname;
        if (nicknameText != null) 
        {
            nicknameText.text = currentNickname;
            
            // Verifica se este objeto de jogador pertence ao cliente local (EU)
            if (photonView.IsMine)
            {
                // JOGADOR LOCAL: Cor Verde
                nicknameText.color = Color.green; 
            }
            else
            {
                // JOGADOR REMOTO (Oponente): Cor Vermelha
                nicknameText.color = Color.red;
            }
        }
    }
}
