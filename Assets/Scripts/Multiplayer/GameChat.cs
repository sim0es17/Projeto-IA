using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;
using System.Linq; // Necessário para algumas extensões, embora não estritamente usado aqui.

public class GameChat : MonoBehaviour
{
    // --- Singleton Pattern ---
    // Instância estática para acesso global (ex: Movement2D.cs, CombatSystem2D.cs)
    public static GameChat instance;

    // --- Propriedade de Acesso ---
    // Informa outros scripts se o jogador está a escrever no chat.
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
    }

    void Update()
    {
        // ----------------------------------------------------
        // BLOQUEIO 1: NO LOBBY (Prioridade Máxima)
        // O chat só pode ser usado depois de o jogo começar.
        // É opcional (verifica se o LobbyManager.instance existe).
        // ----------------------------------------------------
        bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);

        if (lobbyBlocking)
        {
            if (isInputFiieldToggled) CloseChatInput();
            return; 
        }

        // ----------------------------------------------------
        // BLOQUEIO 2: NO PAUSE MENU (Prioridade Média)
        // É opcional (verifica se o PMMM.instance existe).
        // ----------------------------------------------------
        bool isPaused = (PMMM.instance != null && PMMM.IsPausedLocally);

        if (isPaused)
        {
            // Se o jogo for pausado enquanto o chat estava aberto, garante que ele fecha.
            if (isInputFiieldToggled)
            {
                CloseChatInput();
            }
            return; // Bloqueia todo o input de chat enquanto o jogo estiver pausado.
        }


        // ----------------------------------------------------
        // LÓGICA DE ATIVAÇÃO DO CHAT (Tecla 'Y')
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Y) && !isInputFiieldToggled)
        {
            OpenChatInput();
        }

        // ----------------------------------------------------
        // LÓGICA DE DESATIVAÇÃO DO CHAT (Tecla 'Escape')
        // ----------------------------------------------------
        if(Input.GetKeyDown(KeyCode.Escape) && isInputFiieldToggled)
        {
            CloseChatInput();
        }

        // ----------------------------------------------------
        // LÓGICA DE ENVIO DE MENSAGEM (Tecla 'Enter')
        // ----------------------------------------------------
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
        
        // Remove o foco do InputField para que o input do jogador volte ao jogo
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        
        Debug.Log("InputField desativado");
    }
    
    private void SendCurrentMessage()
    {
        // Verifica se estamos conectados para obter o NickName
        string senderName = PhotonNetwork.IsConnected ? PhotonNetwork.LocalPlayer.NickName : "LocalPlayer";
        string messagetoSend = $"{senderName}: {InputField.text}";

        // Envia a mensagem a todos os clientes via RPC (ou apenas chama o SendChatMessage localmente se não estiver na rede)
        PhotonView pv = GetComponent<PhotonView>();
        
        if (pv != null && PhotonNetwork.InRoom)
        {
            pv.RPC("SendChatMessage", RpcTarget.All, messagetoSend);
        }
        else
        {
            // Chamada local se não estivermos em rede ou em sala
            SendChatMessage(messagetoSend); 
        }

        // Limpa o input e fecha
        InputField.text = "";
        CloseChatInput();
    }

    // Método RPC chamado para distribuir a mensagem por todos os clientes
    [PunRPC]
    void SendChatMessage(string _message)
    {
        // Adiciona a nova mensagem ao chatText
        chatText.text = chatText.text + "\n" + _message;
    }
}
