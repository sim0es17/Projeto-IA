using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Necessário para controlar o foco

public class GameChat : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI chatText;
    public TMP_InputField inputField;

    // Estado interno
    private bool isInputFieldToggled = false;
    private RoomManager roomManager;

    void Start()
    {
        roomManager = RoomManager.instance;
        
        // Garante que o input começa escondido
        if (inputField != null)
        {
            inputField.gameObject.SetActive(false);
        }

        // Limpa o chat visual ao iniciar
        if (chatText != null) chatText.text = "";
    }

    void Update()
    {
        // 1. BLOQUEIOS DE SEGURANÇA (Lobby ou Menu)
        // Se estiver no menu de nome OU o jogo ainda não começou (Lobby)...
        if ((roomManager != null && roomManager.IsNamePanelActive) || !LobbyManager.GameStartedAndPlayerCanMove)
        {
            if (isInputFieldToggled) ForceCloseChat();
            return;
        }

        // 2. ABRIR O CHAT (Tecla T)
        if (Input.GetKeyDown(KeyCode.T) && !isInputFieldToggled)
        {
            OpenChat();
        }

        // 3. FECHAR O CHAT (Tecla ESC)
        if (Input.GetKeyDown(KeyCode.Escape) && isInputFieldToggled)
        {
            CloseChat();
        }

        // 4. ENVIAR MENSAGEM (Tecla ENTER)
        // Verifica se carregou no Enter E se o chat está aberto
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && isInputFieldToggled)
        {
            SendMessageLogic();
        }
    }

    // Lógica separada para enviar mensagem para garantir que funciona
    void SendMessageLogic()
    {
        // Verifica se o texto NÃO é vazio ou só espaços
        if (!string.IsNullOrWhiteSpace(inputField.text))
        {
            string messageToSend = $"<b>{PhotonNetwork.LocalPlayer.NickName}:</b> {inputField.text}";
            
            // Envia para todos via RPC
            GetComponent<PhotonView>().RPC("SendChatMessage", RpcTarget.All, messageToSend);
        }

        // IMPORTANTE:
        // Sempre limpa o campo e fecha o chat depois do Enter, mesmo se a msg for vazia.
        // Isso evita que o cursor fique preso.
        inputField.text = "";
        CloseChat();
    }

    void OpenChat()
    {
        isInputFieldToggled = true;
        inputField.gameObject.SetActive(true); 
        inputField.Select();
        inputField.ActivateInputField(); // Força o cursor a aparecer
    }

    void CloseChat()
    {
        isInputFieldToggled = false;
        inputField.DeactivateInputField();
        inputField.gameObject.SetActive(false);
        
        // Retira o foco da UI para o jogador poder voltar a controlar o boneco
        EventSystem.current.SetSelectedGameObject(null);
    }

    void ForceCloseChat()
    {
        inputField.text = ""; // Limpa rascunhos
        CloseChat();
    }

    [PunRPC]
    public void SendChatMessage(string _message)
    {
        chatText.text += _message + "\n";
    }
}
