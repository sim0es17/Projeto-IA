using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;
using System.Linq; 

public class GameChat : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static GameChat instance;

    // --- Propriedade de Acesso ---
    /// <summary>
    /// Informa outros scripts (Movement, Combat) se o jogador está a escrever.
    /// </summary>
    public bool IsChatOpen => isInputFiieldToggled;

    [Header("Referências")]
    public TextMeshProUGUI chatText;
    public TMP_InputField InputField;

    private bool isInputFiieldToggled = false;

    // Implementação do Singleton em Awake
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // O chat deve persistir entre cenas (se for carregada outra cena de jogo).
            DontDestroyOnLoad(this.gameObject); 
        }
        else if (instance != this)
        {
            Destroy(gameObject); 
        }

        // Garante que o input field está desativado no início
        if (InputField != null)
        {
            InputField.DeactivateInputField();
        }
        
        // ** CRUCIAL: Desativa o Canvas do Chat INTEIRO no início **
        gameObject.SetActive(false); 
    }

    // --- NOVO MÉTODO PÚBLICO ---
    /// <summary>
    /// Ativa o Canvas do Chat. Chamado pelo LobbyManager quando o jogo começa.
    /// </summary>
    public void ActivateChatUI()
    {
        gameObject.SetActive(true);
        Debug.Log("[GameChat] Chat UI Ativado! O jogo começou.");
    }

    void Update()
    {
        // Se o Chat não estiver ativo no jogo (ou seja, se o LobbyManager ainda não o ativou), ignorar o input.
        if (!gameObject.activeSelf) return;

        // ----------------------------------------------------
        // BLOQUEIO 1: NO LOBBY (Opcional - verifica se o LobbyManager existe)
        // ----------------------------------------------------
        bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);
        if (lobbyBlocking)
        {
            if (isInputFiieldToggled) CloseChatInput();
            return; 
        }

        // ----------------------------------------------------
        // BLOQUEIO 2: NO PAUSE MENU (Opcional - verifica se o PMMM existe)
        // ----------------------------------------------------
        bool isPaused = (PMMM.instance != null && PMMM.IsPausedLocally);
        if (isPaused)
        {
            if (isInputFiieldToggled)
            {
                CloseChatInput();
            }
            return; 
        }


        // ----------------------------------------------------
        // LÓGICA DE ATIVAÇÃO / DESATIVAÇÃO / ENVIO
        // ----------------------------------------------------
        
        // Ativar o Input Field com 'Y'
        if (Input.GetKeyDown(KeyCode.Y) && !isInputFiieldToggled)
        {
            OpenChatInput();
            return;
        }

        // Desativar o Input Field com 'Escape'
        if(Input.GetKeyDown(KeyCode.Escape) && isInputFiieldToggled)
        {
            CloseChatInput();
            return;
        }

        // Enviar Mensagem com 'Enter'
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && isInputFiieldToggled && !string.IsNullOrEmpty(InputField.text))
        {
            SendCurrentMessage();
        }
    }

    private void OpenChatInput()
    {
        isInputFiieldToggled = true;
        InputField.Select();
        InputField.ActivateInputField();

        Debug.Log("InputField ativado");
    }

    private void CloseChatInput()
    {
        isInputFiieldToggled = false;
        InputField.DeactivateInputField();
        
        // Remove o foco do InputField
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        
        Debug.Log("InputField desativado");
    }
    
    private void SendCurrentMessage()
    {
        // Se estiver conectado, usa o NickName; senão, usa "LocalPlayer" (para testes SP)
        string senderName = PhotonNetwork.IsConnected ? PhotonNetwork.LocalPlayer.NickName : "LocalPlayer";
        string messagetoSend = $"{senderName}: {InputField.text}";

        // Tenta usar o Photon View para enviar o RPC
        PhotonView pv = GetComponent<PhotonView>();
        
        if (pv != null && PhotonNetwork.InRoom)
        {
            // Envia a mensagem a todos os clientes
            pv.RPC("SendChatMessage", RpcTarget.All, messagetoSend);
        }
        else
        {
            // Chamada local (Single Player ou não em sala)
            SendChatMessage(messagetoSend); 
        }

        // Limpa o input e fecha
        InputField.text = "";
        CloseChatInput();
    }

    // Método RPC (ou local) chamado para distribuir a mensagem por todos os clientes
    [PunRPC]
    void SendChatMessage(string _message)
    {
        if (chatText != null)
        {
            // Adiciona a nova mensagem ao chatText
            chatText.text = chatText.text + "\n" + _message;
        }
    }
}
