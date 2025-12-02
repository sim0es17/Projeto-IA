using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI; 
using Hashtable = ExitGames.Client.Photon.Hashtable;
using TMPro;

// --- PROPRIEDADES DE SALA SINCRONIZADAS ---
public static class CustomRoomProperties
{
    public const string StartTime = "st"; 
    public const string GameStarted = "gs"; 
}

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public static LobbyManager instance;

    public static bool GameStartedAndPlayerCanMove = false;

    // --- Configurações de Tempo ---
    private const int MAX_PLAYERS = 4;
    private const float WAIT_TIME_FOR_SECOND_PLAYER = 90f; 
    private const float WAIT_TIME_FULL_ROOM = 5f; 

    // --- Variáveis de Sincronização e Estado ---
    private bool isCountingDown = false;
    private double startTime; 
    private float countdownDuration;
    private float remainingTime;
    private bool hasGameStartedLocally = false; 

    // --- Referências UI (Anexar no Inspector) ---
    [Header("UI Elements")]
    public GameObject lobbyPanel; 
    public TMPro.TextMeshProUGUI countdownText; 
    public TMPro.TextMeshProUGUI playerListText;
    public Button startGameButton; 

    // --- Referência para o Prefab do Chat ---
    [Header("Game References")]
    [Tooltip("Anexar o Prefab da UI do GameChat aqui.")]
    public GameObject gameChatPrefab;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;

        GameStartedAndPlayerCanMove = false;

        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnForceStartGame);
            startGameButton.gameObject.SetActive(false);
        }
    }

    // --- CHAMADO PELO RoomManager QUANDO O JOGADOR ENTRA NA SALA ---
    public void OnRoomEntered()
    {
        if (lobbyPanel == null) return;
        
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
          (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted])
        {
            GameStartLogic();
            return;
        }

        lobbyPanel.SetActive(true);

        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }

        UpdateLobbyUI();
        UpdateCountdownUI(remainingTime);
    }

    // --- UPDATE LOOP (Lógica de Contagem Regressiva) ---
    void Update()
    {
        if (lobbyPanel == null || hasGameStartedLocally) return;

        if (!PhotonNetwork.InRoom || !isCountingDown)
        {
            return;
        }

        double elapsed = PhotonNetwork.Time - startTime;
        elapsed = System.Math.Max(0.0, elapsed); 
        remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);

        UpdateCountdownUI(remainingTime);

        if (PhotonNetwork.IsMasterClient && remainingTime <= 0.01f && startTime > 0) 
        {
            StartGame(); // Chama StartGame no Master
        }
    }

    // --- CALLBACKS DO PHOTON (Sincronização) ---
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

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        // 2. Sincronização do GameStarted (gs)
        if (propertiesThatChanged.ContainsKey(CustomRoomProperties.GameStarted))
        {
            if ((bool)propertiesThatChanged[CustomRoomProperties.GameStarted])
            {
                GameStartLogic(); // Inicia o jogo em todos os clientes
                return;
            }
        }

        // 1. Sincronização do StartTime (st)
        if (!hasGameStartedLocally && propertiesThatChanged.ContainsKey(CustomRoomProperties.StartTime))
        {
            object stValue = propertiesThatChanged[CustomRoomProperties.StartTime];

            if (stValue != null)
            {
                startTime = (double)stValue;
                isCountingDown = true;

                if (PhotonNetwork.CurrentRoom.PlayerCount >= MAX_PLAYERS) countdownDuration = WAIT_TIME_FULL_ROOM; 
                else if (PhotonNetwork.CurrentRoom.PlayerCount >= 2) countdownDuration = WAIT_TIME_FOR_SECOND_PLAYER; 
                
                double elapsed = PhotonNetwork.Time - startTime;
                elapsed = System.Math.Max(0.0, elapsed); 
                remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);
            }
            else StopCountdown();
        }

        UpdateLobbyUI();
        UpdateCountdownUI(remainingTime);
    }


    // --- LÓGICA DE INÍCIO DE JOGO (MASTER CLIENT APENAS) ---
    private void CheckStartConditions()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        
        bool gameAlreadyStarted = false;
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted))
        {
            gameAlreadyStarted = (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted];
        }

        if (gameAlreadyStarted) return;

        if (currentPlayers >= MAX_PLAYERS)
        {
            if (!isCountingDown || countdownDuration != WAIT_TIME_FULL_ROOM) SetStartTime(WAIT_TIME_FULL_ROOM);
        }
        else if (currentPlayers >= 2)
        {
            if (!isCountingDown || countdownDuration != WAIT_TIME_FOR_SECOND_PLAYER) SetStartTime(WAIT_TIME_FOR_SECOND_PLAYER);
        }
        else
        {
            StopCountdown();
        }
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
        
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
          (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted]) return;

        isCountingDown = false;

        Hashtable props = new Hashtable { { CustomRoomProperties.GameStarted, true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        startTime = 0; 
        remainingTime = 0f; 
        
        // CHAMA O ROOM MANAGER PARA CONTINUAR A LÓGICA DE INÍCIO
        if (RoomManager.instance != null)
        {
            RoomManager.instance.StartGame(); 
        }
        Debug.Log("[Lobby] Jogo iniciado pelo Master Client.");
    }

    // --- LÓGICA DE TRANSIÇÃO PARA O JOGO (TODOS OS CLIENTES) ---
    private void GameStartLogic()
    {
        if (hasGameStartedLocally) return;
        hasGameStartedLocally = true;

        GameStartedAndPlayerCanMove = true; 

        if (lobbyPanel != null) lobbyPanel.SetActive(false);

        // DESLIGA A CÂMARA VIA ROOM MANAGER
        if (RoomManager.instance != null && RoomManager.instance.roomCam != null)
        {
            RoomManager.instance.roomCam.SetActive(false);
        }
        
        // INSTANCIA O CHAT
        if (gameChatPrefab != null) Instantiate(gameChatPrefab);

        // CONFIGURA VIDAS INICIAIS
        if (RoomManager.instance != null)
        {
            RoomManager.instance.SetInitialRespawnCount(PhotonNetwork.LocalPlayer);
        }

        if (PhotonNetwork.IsMasterClient) PhotonNetwork.CurrentRoom.IsOpen = false;

        Debug.Log("[Lobby] Lógica de Início de Jogo local executada.");
    }

    // --- LÓGICA DA UI ---
    private void UpdateLobbyUI()
    {
        if (!PhotonNetwork.InRoom || playerListText == null) return;

        string players = $"Jogadores na Sala ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS}):\n";
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
        {
            string nick = string.IsNullOrEmpty(p.NickName) ? $"Player {p.ActorNumber}" : p.NickName;
            players += $"- **{nick}** {(p.IsMasterClient ? "(Host)" : "")}\n";
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

        if (hasGameStartedLocally)
        {
            countdownText.text = ""; 
            return;
        }

        if (!isCountingDown || time <= 0)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2) countdownText.text = $"Aguardando 1º jogador...\n(Mínimo de 2 para começar)";
            else countdownText.text = $"Partida em espera. Contagem regressiva parada.";
            return;
        }

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (PhotonNetwork.CurrentRoom.PlayerCount >= MAX_PLAYERS)
        {
            countdownText.text = $"SALA CHEIA! Partida começa em: \n**{timeString}**";
        }
        else
        {
            countdownText.text = $"Início da Partida em: \n**{timeString}**";
        }
    }
}
