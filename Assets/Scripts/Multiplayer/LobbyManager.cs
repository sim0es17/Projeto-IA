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
    // Chave para a hora de início da contagem regressiva (double - PhotonNetwork.Time)
    public const string StartTime = "st"; 
    // Chave booleana que indica que a partida começou
    public const string GameStarted = "gs"; 
}

public class LobbyManager : MonoBehaviourPunCallbacks
{
    // --- Singleton Pattern ---
    public static LobbyManager instance;

    // --- Variável Estática para Bloqueio de Input ---
    /// <summary>
    /// Flag estática que o Movement2D e o GameChat verificam para saber se o input de jogo está ativo.
    /// </summary>
    public static bool GameStartedAndPlayerCanMove = false;

    // --- Configurações de Tempo ---
    private const int MAX_PLAYERS = 4;
    private const float WAIT_TIME_FOR_SECOND_PLAYER = 90f; // Tempo quando há 2 ou mais jogadores
    private const float WAIT_TIME_FULL_ROOM = 5f; // Tempo quando a sala está cheia

    // --- Variáveis de Sincronização e Estado ---
    private bool isCountingDown = false;
    private double startTime; // Sincronizado pela rede
    private float countdownDuration;
    private float remainingTime;
    private bool hasGameStartedLocally = false; // Flag local para garantir que a GameStartLogic corre uma vez

    // --- Referências UI (Anexar no Inspector) ---
    [Header("UI Elements")]
    public GameObject lobbyPanel; 
    public TMPro.TextMeshProUGUI countdownText; 
    public TMPro.TextMeshProUGUI playerListText;
    public Button startGameButton; // Botão para Host forçar o início

    // --- Referência para o Prefab do Chat (CRUCIAL) ---
    [Header("Game References")]
    [Tooltip("Anexar o Prefab da UI do GameChat aqui.")]
    public GameObject gameChatPrefab;

    private void Awake()
    {
        // Implementação do Singleton
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;

        // O estado inicial é sempre FALSO
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
        
        // Se a propriedade de sala indicar que o jogo já começou, inicia imediatamente
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
            (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted])
        {
            GameStartLogic();
            return;
        }

        lobbyPanel.SetActive(true);

        // Apenas o Master Client verifica e define a contagem
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
        // Só corre se estivermos no lobby e o jogo ainda não tiver começado localmente
        if (lobbyPanel == null || hasGameStartedLocally) return;

        if (!PhotonNetwork.InRoom || !isCountingDown)
        {
            return;
        }

        // Calcula o tempo restante usando o tempo de rede (sincronizado)
        double elapsed = PhotonNetwork.Time - startTime;
        elapsed = System.Math.Max(0.0, elapsed); 
        remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);

        UpdateCountdownUI(remainingTime);

        // O Master Client é responsável por iniciar o jogo quando o tempo acaba
        if (PhotonNetwork.IsMasterClient && remainingTime <= 0.01f && startTime > 0) 
        {
            StartGame();
        }
    }

    // --- CALLBACKS DO PHOTON (Sincronização) ---

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        // O Master Client reavalia as condições de início
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }
        UpdateLobbyUI(); 
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }
        UpdateLobbyUI(); 
    }

    // A chave para sincronizar o início da contagem regressiva e o início do jogo
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        // 2. Sincronização do GameStarted (gs)
        if (propertiesThatChanged.ContainsKey(CustomRoomProperties.GameStarted))
        {
            if ((bool)propertiesThatChanged[CustomRoomProperties.GameStarted])
            {
                // Todos os clientes que recebem esta propriedade iniciam o jogo
                GameStartLogic();
                return;
            }
        }

        // 1. Sincronização do StartTime (st) - Usado para cronometrar a contagem regressiva
        if (!hasGameStartedLocally && propertiesThatChanged.ContainsKey(CustomRoomProperties.StartTime))
        {
            object stValue = propertiesThatChanged[CustomRoomProperties.StartTime];

            if (stValue != null)
            {
                startTime = (double)stValue;
                isCountingDown = true;

                // Redefine a duração da contagem com base no número de jogadores (Master Client já o fez, mas é preciso reconfirmar)
                if (PhotonNetwork.CurrentRoom.PlayerCount >= MAX_PLAYERS)
                {
                    countdownDuration = WAIT_TIME_FULL_ROOM; 
                }
                else if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
                {
                    countdownDuration = WAIT_TIME_FOR_SECOND_PLAYER; 
                }
                
                // Calcula o tempo restante para o cliente que acabou de receber a propriedade
                double elapsed = PhotonNetwork.Time - startTime;
                elapsed = System.Math.Max(0.0, elapsed); 
                remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);

                Debug.Log($"[Lobby] Timer sincronizado. Restante Inicial: {remainingTime:0.0}s.");
            }
            else // Caso o Master Client tenha parado a contagem (props == null)
            {
                StopCountdown();
            }
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
            // Sala cheia -> contagem curta
            if (!isCountingDown || countdownDuration != WAIT_TIME_FULL_ROOM)
            {
                SetStartTime(WAIT_TIME_FULL_ROOM);
            }
        }
        else if (currentPlayers >= 2)
        {
            // Mínimo atingido -> contagem longa
            if (!isCountingDown || countdownDuration != WAIT_TIME_FOR_SECOND_PLAYER)
            {
                SetStartTime(WAIT_TIME_FOR_SECOND_PLAYER);
            }
        }
        else
        {
            // Jogador único -> para a contagem
            StopCountdown();
        }
    }

    private void SetStartTime(float duration)
    {
        // Define a hora de início na rede (sincroniza o timer para todos)
        if (isCountingDown && Mathf.Approximately(countdownDuration, duration))
        {
            return;
        }

        countdownDuration = duration;
        startTime = PhotonNetwork.Time;

        Hashtable props = new Hashtable
        {
            { CustomRoomProperties.StartTime, startTime }
        };

        // Envia a propriedade para a sala
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        isCountingDown = true;
    }

    private void StopCountdown()
    {
        // Define a propriedade para null para parar o timer em todos os clientes
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
        if (PhotonNetwork.IsMasterClient)
        {
            StartGame();
        }
    }

    // Master Client inicia o jogo e sincroniza a propriedade 'GameStarted'
    private void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
            (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted])
        {
            return;
        }

        isCountingDown = false;

        // A CHAVE: Define 'gs' como true, o que aciona GameStartLogic em TODOS os clientes via OnRoomPropertiesUpdate
        Hashtable props = new Hashtable { { CustomRoomProperties.GameStarted, true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        startTime = 0; 
        remainingTime = 0f; 
        
        Debug.Log("[Lobby] Jogo iniciado pelo Master Client.");
    }

    // --- LÓGICA DE TRANSIÇÃO PARA O JOGO (TODOS OS CLIENTES) ---

    private void GameStartLogic()
    {
        if (hasGameStartedLocally) return;
        hasGameStartedLocally = true;

        // 1. ** CRUCIAL ** Desbloqueia o input e movimento (flag estática)
        GameStartedAndPlayerCanMove = true; 

        // 2. Desativa a tela de espera
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }

        // 3. Desativa a câmera do Lobby
        if (RoomManager.instance != null && RoomManager.instance.roomCam != null)
        {
            RoomManager.instance.roomCam.SetActive(false);
        }
        
        // 4. INSTANCIA O CHAT
        if (gameChatPrefab != null)
        {
            // O chat é criado AGORA, depois de GameStartedAndPlayerCanMove ser TRUE.
            // O chat é um objeto UI LOCAL (não de rede, pois é uma tela/HUD)
            Instantiate(gameChatPrefab); 
            Debug.Log("[Lobby] GameChat UI Instanciado.");
        }

        // 5. Permite que o RoomManager spawne o jogador local.
        if (RoomManager.instance != null)
        {
            RoomManager.instance.SetInitialRespawnCount(PhotonNetwork.LocalPlayer);
            RoomManager.instance.RespawnPlayer();
        }

        // 6. Fechea sala para novos jogadores (Master Client apenas)
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }

        Debug.Log("[Lobby] Lógica de Início de Jogo local executada.");
    }

    // --- LÓGICA DA UI ---

    private void UpdateLobbyUI()
    {
        if (!PhotonNetwork.InRoom) return;
        if (playerListText == null) return;

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
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
            {
                countdownText.text = $"Aguardando 1º jogador...\n(Mínimo de 2 para começar)";
            }
            else 
            {
                countdownText.text = $"Partida em espera. Contagem regressiva parada.";
            }
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
