using UnityEngine;
using TMPro;
using Photon.Pun;
using System.Collections.Generic;
using ExitGames.Client.Photon; // Necessário para RaiseEvent e EventData
using Photon.Realtime; // Necessário para IOnEventCallback

// --- DEFINIÇÃO DO EVENTO CUSTOMIZADO ---
// Código de evento que será usado para o chat. Deve ser único (e menor que 200).
public static class ChatEventCodes
{
    public const byte ChatMessage = 1;
}

// O script implementa IOnEventCallback para receber o evento de chat
public class GameChat : MonoBehaviour, IOnEventCallback 
{
    // --- Singleton Pattern ---
    public static GameChat instance;
    
    [Header("UI References")]
    [Tooltip("O InputField para escrever a mensagem.")]
    public TMP_InputField inputField;
    [Tooltip("O TextMeshProUGUI onde as mensagens são exibidas.")]
    public TextMeshProUGUI chatDisplay;
    
    // --- Configurações ---
    private const int MAX_MESSAGES = 10;
    
    // Variável interna que indica se o campo de input está ativo
    private bool isInputFieldToggled = false;
    
    // Lista local para armazenar as mensagens
    private List<string> messageHistory = new List<string>();

    // --- Referências a Singletons ---
    private RoomManager roomManager;
    private PMMM pauseManager;

    // --- PROPRIEDADE PÚBLICA (USADA POR CombatSystem2D) ---
    /// <summary>
    /// Retorna o estado atual do Input Field do chat. Usado para bloquear inputs de movimento/ataque.
    /// </summary>
    public bool IsChatOpen 
    {
        get { return isInputFieldToggled; }
    }


    void Awake()
    {
        // Implementação do Singleton
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        
        // Adiciona um listener para o Enter/Return no InputField
        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(delegate { OnInputFieldEndEdit(inputField.text); });
        }
    }
    
    void OnEnable()
    {
        // OBRIGATÓRIO: Subscrever o callback de eventos da Photon quando o objeto é ativado
        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {
        // OBRIGATÓRIO: Remover a subscrição quando o objeto é desativado/destruído
        PhotonNetwork.RemoveCallbackTarget(this);
    }
    
    void Start()
    {
        // Obtém as referências dos Singletons no Start
        roomManager = RoomManager.instance;
        pauseManager = PMMM.instance;
        
        // Garante que o input field começa fechado
        ForceCloseChat();
        
        // Garante que o chat fica limpo
        UpdateChatDisplay();
    }

    void Update()
    {
        // ------------------------------------
        // LÓGICA DE BLOQUEIO GERAL
        // ------------------------------------
        
        // Condições que SEMPRE bloqueiam o chat: Menu de Pausa ou Painel de Nome.
        bool isBlockedByUI = (roomManager != null && roomManager.IsNamePanelActive) ||
            (pauseManager != null && PMMM.IsPausedLocally); 
        if (isBlockedByUI)
        {
            Debug.LogWarning("[GameChat] Bloqueado pela UI (Painel de Nome ou Pausa).");
            if (isInputFieldToggled) ForceCloseChat();
            return;
        }

        // Se não estivermos no lobby E o jogo ainda não começou, bloqueia.
        if (!PhotonNetwork.InLobby && !LobbyManager.GameStartedAndPlayerCanMove)
        {
            Debug.LogWarning("[GameChat] Bloqueado: Não está no lobby e o jogo não começou.");
            return;
        }

        // ------------------------------------
        // LÓGICA DE ABRIR/FECHAR O CHAT
        // ------------------------------------

        // Se o chat está aberto, a única tecla que nos interessa é ESCAPE para fechar.
        if (isInputFieldToggled)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ForceCloseChat();
            }
        }
        // Se o chat está fechado, a única tecla que nos interessa é 'T' para abrir.
        else 
        {
            if (Input.GetKeyDown(KeyCode.T)) {
                Debug.Log("[GameChat] Tecla 'T' pressionada. Abrindo o chat...");
                OpenChatInput();
            }
        }
    }

    // --- FUNÇÕES DE CONTROLO DO INPUT FIELD ---

    private void OpenChatInput()
    {
        if (inputField == null)
        {
            Debug.LogError("GAMECHAT ERROR: InputField está null. Verifique a referência no Inspector.");
            return;
        }
        
        isInputFieldToggled = true;
        inputField.gameObject.SetActive(true);
        inputField.text = ""; 
        inputField.ActivateInputField(); // Foca o input field
        
        // Se o jogo estiver a correr, liberta o cursor para poder clicar no chat/UI
        if (pauseManager != null)
        {
            pauseManager.UnlockCursor();
        }
        Debug.Log("DEBUG CHAT: OpenChatInput executado. InputField ativado.");
    }

    /// <summary>
    /// Fecha o input field do chat, limpa o texto e volta a confinar o cursor.
    /// </summary>
    public void ForceCloseChat()
    {
        if (inputField == null) return;

        isInputFieldToggled = false;
        inputField.gameObject.SetActive(false); 
        inputField.DeactivateInputField(); // Tira o foco do input field

        // Se o jogo não estiver em pausa, confina o cursor
        if (pauseManager != null && !PMMM.IsPausedLocally)
        {
            pauseManager.LockCursor();
        }
    }

    // --- LÓGICA DE ENVIO DE MENSAGENS (COM RAISE EVENT) ---

    public void OnInputFieldEndEdit(string message)
    {
        // 1. Apenas processamos se a edição terminou com ENTER/RETURN
        if (!inputField.gameObject.activeSelf || 
            (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            ForceCloseChat();
            return;
        }
        
        // 2. Verifica se a mensagem é válida
        if (string.IsNullOrWhiteSpace(message))
        {
            ForceCloseChat();
            return;
        }

        // 3. ENVIA A MENSAGEM VIA PHOTON NETWORK RAISE EVENT
        string senderName = PhotonNetwork.NickName;
        // Cria o array de dados (senderName e message)
        object[] data = new object[] { senderName, message };

        // Opções de Envio (garante que todos recebem, incluindo quem enviou)
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        SendOptions sendOptions = new SendOptions { Reliability = true };
        
        // Envia o evento
        PhotonNetwork.RaiseEvent(ChatEventCodes.ChatMessage, data, raiseEventOptions, sendOptions);
        
        // 4. Fecha o chat e limpa (pronto para a próxima)
        ForceCloseChat();
    }
    
    // --- LÓGICA DE RECEÇÃO DE MENSAGENS (IOnEventCallback) ---

    public void OnEvent(EventData photonEvent)
    {
        // Verifica se o evento recebido é o nosso evento de chat
        if (photonEvent.Code == ChatEventCodes.ChatMessage)
        {
            // O conteúdo da mensagem (data) é um array de objects
            object[] data = (object[])photonEvent.CustomData;

            // Extrai a informação
            string senderName = (string)data[0];
            string message = (string)data[1];
            
            // Processa a mensagem
            ProcessMessage(senderName, message);
        }
    }
    
    /// <summary>
    /// Processa a mensagem recebida e atualiza o display
    /// </summary>
    private void ProcessMessage(string senderName, string message)
    {
        // Formata a mensagem com o nome do remetente
        string formattedMessage = $"**[{senderName}]**: {message}";
        
        // Adiciona ao histórico
        messageHistory.Add(formattedMessage);

        // Limita o histórico de mensagens
        if (messageHistory.Count > MAX_MESSAGES)
        {
            messageHistory.RemoveAt(0); // Remove a mensagem mais antiga
        }

        // Atualiza a exibição
        UpdateChatDisplay();
    }

    private void UpdateChatDisplay()
    {
        if (chatDisplay == null) return;

        // Constrói o texto a partir do histórico
        string displayContent = "";
        foreach (string msg in messageHistory)
        {
            displayContent += msg + "\n";
        }

        chatDisplay.text = displayContent;
    }
}
