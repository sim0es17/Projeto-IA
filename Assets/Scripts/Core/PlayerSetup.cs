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

    // NOVO: Referências de sincronização
    private PhotonView photonView;
    private Animator anim;

    // Variáveis usadas para interpolar animação em clientes remotos
    private float syncSpeed;
    private bool syncGrounded;

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        anim = GetComponent<Animator>();

        // Apenas o cliente local deve processar o input, ativar a câmara, etc.
        if (photonView.IsMine)
        {
            // Ativa o controle e a câmara
            IsLocalPlayer();
        }
        else // Cliente Remoto
        {
            // Desativa o controle e o combate nos clientes remotos,
            // a posição é sincronizada pelo PhotonRigidbody2DView
            movement.enabled = false;
            if (combat != null) combat.enabled = false;

            // Certifique-se de que a câmara não está ativa em jogadores remotos
            if (camara != null) camara.SetActive(false);
        }
    }

    // Chamado pelo Room Manager para configurar o jogador local
    public void IsLocalPlayer()
    {
        // Ativa os scripts de input
        movement.enabled = true;

        // Ativa a câmara
        if (camara != null)
        {
            camara.SetActive(true);

            // LÓGICA DO SCRIPT ORIGINAL INTEGRADA AQUI:
            // Inicia o zoom dinâmico quando a câmara for ativada
            var zoomDynamic = camara.GetComponent<CameraDynamicZoom>();
            if (zoomDynamic != null)
                zoomDynamic.enabled = true;
        }

        // Enable combat system for the local player only
        if (combat != null)
            combat.enabled = true;
    }

    // --- SINCRONIZAÇÃO DE ANIMAÇÃO PARA JOGADORES REMOTOS ---

    void Update()
    {
        // Se não for o seu objeto, aplica as animações sincronizadas
        if (!photonView.IsMine && anim)
        {
            // Aplica os valores sincronizados recebidos no OnPhotonSerializeView
            anim.SetFloat("Speed", Mathf.Abs(syncSpeed));
            anim.SetBool("Grounded", syncGrounded);
        }
    }

    /// <summary>
    /// Sincroniza o estado de animação pela rede.
    /// </summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // --- O JOGADOR LOCAL ESTÁ A ENVIAR DADOS ---

            // Enviar velocidade horizontal e estado no chão (Propriedades do Movement2D)
            stream.SendNext(movement.CurrentHorizontalSpeed);
            stream.SendNext(movement.IsGrounded);
        }
        else
        {
            // --- O JOGADOR REMOTO ESTÁ A RECEBER DADOS ---

            // Receber velocidade horizontal e estado no chão
            this.syncSpeed = (float)stream.ReceiveNext();
            this.syncGrounded = (bool)stream.ReceiveNext();

            // A interpolação (suavização) da posição física é tratada pelo PhotonRigidbody2DView.
        }
    }

    [PunRPC]
    public void SetNickname(string _nickname)
    {
        nickname = _nickname;
        nicknameText.text = nickname;
    }
}
