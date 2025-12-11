using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using TMPro;

public static class CustomRoomProperties
{
    public const string StartTime = "st";
    public const string GameStarted = "gs";
}

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public static LobbyManager instance;
    public static bool GameStartedAndPlayerCanMove = false;

    private const int MAX_PLAYERS = 4;
    private const float WAIT_TIME_FOR_SECOND_PLAYER = 90f;
    private const float WAIT_TIME_FULL_ROOM = 5f;

    private bool isCountingDown = false;
    private double startTime;
    private float countdownDuration;
    private float remainingTime;
    private bool hasGameStartedLocally = false;

    [Header("UI Elements")]
    public GameObject lobbyPanel;
    public TMPro.TextMeshProUGUI countdownText;
    public TMPro.TextMeshProUGUI playerListText;
    public Button startGameButton;

    [Header("Game References")]
    // Objeto do GameChat que já está na cena, desativado no Inspector
    public GameObject gameChatObject; 

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this.gameObject); return; }
        instance = this;
        GameStartedAndPlayerCanMove = false;

        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnForceStartGame);
            startGameButton.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (lobbyPanel == null || hasGameStartedLocally) return;
        if (!PhotonNetwork.InRoom || !isCountingDown) return;

        double elapsed = PhotonNetwork.Time - startTime;
        elapsed = System.Math.Max(0.0, elapsed);
        remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);

        UpdateCountdownUI(remainingTime);

        // Apenas o Master Client inicia o jogo quando o tempo acaba
        if (PhotonNetwork.IsMasterClient && remainingTime <= 0.01f && startTime > 0)
        {
            StartGame();
        }
    }

    public void OnRoomEntered()
    {
        if (lobbyPanel == null) return;

        // Se o jogo já começou na sala (propriedade "gs"), pula o lobby
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
            (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted])
        {
            GameStartLogic();
            return;
        }

        // DESATIVA A TELA DE NOME (NameScreen) ao entrar no Lobby
        if (RoomManager.instance != null && RoomManager.instance.nameUI != null)
        {
            RoomManager.instance.nameUI.SetActive(false);
        }

        lobbyPanel.SetActive(true);
        
        // Ativação do Chat
        ToggleChatVisibility(true); 

        if (PhotonNetwork.IsMasterClient) CheckStartConditions();
        
        // CHAMA O UPDATE PARA EXIBIR O NICKNAME (definido no RoomManager)
        UpdateLobbyUI(); 
        UpdateCountdownUI(remainingTime);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        if (PhotonNetwork.IsMasterClient) CheckStartConditions();
        UpdateLobbyUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (PhotonNetwork.IsMasterClient) CheckStartConditions();
        UpdateLobbyUI();
    }
    
    // Este override garante que o nome do jogador atualiza para todos se ele mudar
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // Se o Photon NickName for alterado por qualquer motivo (raro), atualiza o lobby
        UpdateLobbyUI(); 
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        // Se a propriedade de Jogo Iniciado for alterada para TRUE
        if (propertiesThatChanged.ContainsKey(CustomRoomProperties.GameStarted))
        {
            if ((bool)propertiesThatChanged[CustomRoomProperties.GameStarted])
            {
                GameStartLogic();
                return;
            }
        }

        // Se a propriedade de StartTime for alterada (iniciando/parando contagem regressiva)
        if (!hasGameStartedLocally && propertiesThatChanged.ContainsKey(CustomRoomProperties.StartTime))
        {
            object stValue = propertiesThatChanged[CustomRoomProperties.StartTime];

            if (stValue != null)
            {
                startTime = (double)stValue;
                isCountingDown = true;

                if (PhotonNetwork.CurrentRoom.PlayerCount >= MAX_PLAYERS) countdownDuration = WAIT_TIME_FULL_ROOM;
                else if (PhotonNetwork.CurrentRoom.PlayerCount >= 2) countdownDuration = WAIT_TIME_FOR_SECOND_PLAYER;

                // Calcula o tempo restante baseado no tempo de rede
                double elapsed = PhotonNetwork.Time - startTime;
                elapsed = System.Math.Max(0.0, elapsed);
                remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);
            }
            else
            {
                StopCountdown();
            }
        }
        UpdateLobbyUI();
        UpdateCountdownUI(remainingTime);
    }

    private void CheckStartConditions()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        bool gameAlreadyStarted = false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted))
            gameAlreadyStarted = (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted];

        if (gameAlreadyStarted) return;

        if (currentPlayers >= MAX_PLAYERS)
        {
            if (!isCountingDown || countdownDuration != WAIT_TIME_FULL_ROOM) SetStartTime(WAIT_TIME_FULL_ROOM);
        }
        else if (currentPlayers >= 2)
        {
            if (!isCountingDown || countdownDuration != WAIT_TIME_FOR_SECOND_PLAYER) SetStartTime(WAIT_TIME_FOR_SECOND_PLAYER);
        }
        else StopCountdown();
    }

    private void SetStartTime(float duration)
    {
        if (isCountingDown && Mathf.Approximately(countdownDuration, duration)) return;

        countdownDuration = duration;
        startTime = PhotonNetwork.Time;
        Hashtable props = new Hashtable { { CustomRoomProperties.StartTime, startTime } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        isCountingDown = true;
    }

    private void StopCountdown()
    {
        if (isCountingDown)
        {
            isCountingDown = false;
            // Define a propriedade para nulo para sinalizar a parada
            Hashtable props = new Hashtable { { CustomRoomProperties.StartTime, null } }; 
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            startTime = 0;
            remainingTime = 0f;
        }
    }

    public void OnForceStartGame()
    {
        if (PhotonNetwork.IsMasterClient) StartGame();
    }

    private void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Verifica se o jogo já está marcado como iniciado
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
            (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted]) return;

        // Define a propriedade de sala para True (inicia o jogo para todos)
        isCountingDown = false;
        Hashtable props = new Hashtable { { CustomRoomProperties.GameStarted, true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        startTime = 0;
        remainingTime = 0f;
        Debug.Log("[Lobby] Jogo iniciado pelo Master Client.");
    }

    private void GameStartLogic()
    {
        if (hasGameStartedLocally) return;
        hasGameStartedLocally = true;

        GameStartedAndPlayerCanMove = true;

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        
        // Desativação do Chat
        ToggleChatVisibility(false); 

        // === INICIA O JOGO NO ROOM MANAGER ===
        if (RoomManager.instance != null)
        {
            RoomManager.instance.StartGame();
        }
        else
        {
            Debug.LogError("RoomManager não encontrado! Impossível iniciar o jogo.");
        }

        // Trava a sala para novos jogadores
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.CurrentRoom.IsOpen = false;

        Debug.Log("[Lobby] Jogo iniciado localmente.");
    }

    // Função para controlar a visibilidade do Chat
    private void ToggleChatVisibility(bool isVisible)
    {
        if (gameChatObject != null)
        {
            gameChatObject.SetActive(isVisible);
        }
    }
    
    private void UpdateLobbyUI()
    {
        if (!PhotonNetwork.InRoom) return;
        if (playerListText == null) return;

        string players = $"Players in the Room ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS}):\n";
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
        {
            // O nome vem do PhotonNetwork.NickName, definido no RoomManager
            string nick = string.IsNullOrEmpty(p.NickName) ? $"Player {p.ActorNumber}" : p.NickName;
            
            // Adicionamos um indicador para o próprio jogador (você) para ser mais claro
            bool isLocal = p.IsLocal;
            
            players += $"- {nick} {(p.IsMasterClient ? "(Host)" : "")}{(isLocal ? " (You)" : "")}\n";
        }
        playerListText.text = players;

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient && !hasGameStartedLocally);
        }
    }

    private void UpdateCountdownUI(float time)
    {
        if (countdownText == null) return;
        if (hasGameStartedLocally) { countdownText.text = ""; return; }

        if (!isCountingDown || time <= 0)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2) countdownText.text = $"Waiting for 2nd player...\n(Minimum 2 to start)";
            else countdownText.text = $"Departure on hold. Counting stopped.";
            return;
        }

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (PhotonNetwork.CurrentRoom.PlayerCount >= MAX_PLAYERS) countdownText.text = $"FULL ROOM! Start in: \n{timeString}";
        else countdownText.text = $"Start of the Match in: \n{timeString}";
    }
}
