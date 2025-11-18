using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using TMPro;

// Define a chave para as Custom Room Properties (Chaves usadas para sincronizar dados da sala)
public static class CustomRoomProperties
{
    // Usar chaves curtas como "st" e "gs" ajuda a reduzir o tráfego de rede
    public const string StartTime = "st";       // Timestamp (double) de quando o timer começou
    public const string GameStarted = "gs";     // Sinaliza se o jogo começou (bool)
}

public class LobbyManager : MonoBehaviourPunCallbacks
{
    // --- Singleton Pattern ---
    public static LobbyManager instance;

    // --- NOVO FLAG PARA CONTROLE DE MOVIMENTO ---
    public static bool GameStartedAndPlayerCanMove = false;

    // --- Configurações de Tempo ---
    private const int MAX_PLAYERS = 4;
    private const float WAIT_TIME_FOR_SECOND_PLAYER = 90f; // 90 segundos para 2+ jogadores
    private const float WAIT_TIME_FULL_ROOM = 5f;          // 5 segundos se a sala estiver cheia (4/4)

    // --- Variáveis de Sincronização e Estado ---
    private bool isCountingDown = false;
    private double startTime;             // O tempo real do Photon em que a contagem começou
    private float countdownDuration;      // Duração total da contagem regressiva (90s ou 5s)
    private float remainingTime;          // Tempo restante no countdown

    // --- Referências UI (Anexar no Inspector) ---
    [Header("UI Elements")]
    public GameObject lobbyPanel;         // O painel que contém toda a UI do lobby (ativar/desativar)
    public TMPro.TextMeshProUGUI countdownText;      // Texto para mostrar o tempo restante
    public TMPro.TextMeshProUGUI playerListText;     // Texto para mostrar a lista de jogadores

    private void Awake()
    {
        // Garante que haja apenas uma instância do LobbyManager
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;

        // Garante que o movimento esteja desativado por padrão ao carregar a cena
        GameStartedAndPlayerCanMove = false;
    }

    // --- ENTRADA NO LOBBY ---

    // Chamada pelo RoomManager após o jogador entrar na sala (OnJoinedRoom)
    public void OnRoomEntered()
    {
        // **Verificação de segurança para UI**
        if (lobbyPanel == null) return;

        // Ativa a tela de espera/lobby
        lobbyPanel.SetActive(true);

        // MasterClient verifica imediatamente as condições de início
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }

        // Garante que a UI seja inicializada
        UpdateLobbyUI();
        // Garante que o timer UI comece no estado correto
        UpdateCountdownUI(remainingTime);
    }

    // --- UPDATE LOOP (Lógica de Contagem) ---
    void Update()
    {
        // CORREÇÃO DE ERRO: Garante que o painel de UI não foi destruído
        if (lobbyPanel == null) return;

        // Só executa a lógica de contagem se estiver na sala e o timer estiver ativo
        if (!PhotonNetwork.InRoom || !isCountingDown)
        {
            // A UI ainda precisa ser atualizada fora do countdown para mostrar a lista de jogadores
            if (lobbyPanel.activeSelf) UpdateLobbyUI();
            return;
        }

        // Lógica de contagem regressiva (compartilhada por todos os clientes)
        // Usa o tempo sincronizado do Photon
        double elapsed = PhotonNetwork.Time - startTime;

        // --- CORREÇÃO APLICADA (Linha 101) ---
        // Usamos System.Math.Max porque 'elapsed' é double e Mathf é para float
        elapsed = System.Math.Max(0.0, elapsed); 
        
        remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);

        Debug.Log($"Contagem regressiva: {remainingTime}s restantes.");

        // Atualiza a UI para todos os clientes
        UpdateCountdownUI(remainingTime);

        // APENAS o MasterClient decide o que fazer quando o tempo acaba
        if (PhotonNetwork.IsMasterClient && remainingTime <= 0.01f && startTime > 0) 
        {
            StartGame();
        }
    }

    // --- CALLBACKS DO PHOTON (Para MasterClient Controlar o Estado) ---

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        Debug.Log("Player entrou: " + newPlayer.NickName);
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }
        UpdateLobbyUI(); // Atualiza a lista de jogadores.
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }
        UpdateLobbyUI(); // Atualiza a lista de jogadores para todos
    }

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

        // Verificação para evitar que o timer recomece após o jogo começar
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
            (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted])
        {
            UpdateLobbyUI();
            return;
        }

        // 1. Sincronização do StartTime (st)
        if (propertiesThatChanged.ContainsKey(CustomRoomProperties.StartTime))
        {
            object stValue = propertiesThatChanged[CustomRoomProperties.StartTime];

            if (stValue != null)
            {
                startTime = (double)stValue;

                // Determina a duração correta com base no número de jogadores no momento
                if (PhotonNetwork.CurrentRoom.PlayerCount >= MAX_PLAYERS)
                {
                    countdownDuration = WAIT_TIME_FULL_ROOM; // 5s se estiver cheio
                }
                else if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
                {
                    countdownDuration = WAIT_TIME_FOR_SECOND_PLAYER; // 90s se tiver 2+
                }

                isCountingDown = true;
                
                // --- CORREÇÃO APLICADA (Linha 188) ---
                double elapsed = PhotonNetwork.Time - startTime;
                elapsed = System.Math.Max(0.0, elapsed); // Usamos System.Math para doubles

                remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);

                Debug.Log($"Timer sincronizado. Duração: {countdownDuration}s. Início: {startTime}. Tempo Restante Inicial: {remainingTime}s.");
            }
            else
            {
                // Se o valor for null, significa que o MasterClient parou o timer
                isCountingDown = false;
                startTime = 0; // Resetar startTime quando o timer para.
                remainingTime = 0f; // Resetar remainingTime
            }
        }

        // Atualiza a UI para todos os clientes (lista de jogadores, etc.)
        UpdateLobbyUI();
        // Forçar atualização da UI do timer após qualquer propriedade de sala mudar
        UpdateCountdownUI(remainingTime);
    }


    // --- LÓGICA DE INÍCIO DE JOGO (MASTER CLIENT APENAS) ---

    // Verifica as condições de início e ajusta o timer (APENAS MasterClient)
    private void CheckStartConditions()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("Passed master client verifiation");

        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;

        Debug.Log(currentPlayers + " players in room.");

        // Obter o estado atual da propriedade GameStarted da sala para evitar reiniciar o timer se o jogo já começou
        bool gameAlreadyStarted = false;
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted))
        {
            gameAlreadyStarted = (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted];
        }

        if (gameAlreadyStarted)
        {
            // O jogo já começou, não precisamos mais verificar as condições de início do timer.
            Debug.Log("Game already started, not adjusting timer conditions.");
            return;
        }

        if (currentPlayers >= MAX_PLAYERS)
        {
            // 5 segundos se a sala estiver cheia.
            if (!isCountingDown || countdownDuration != WAIT_TIME_FULL_ROOM)
            {
                SetStartTime(WAIT_TIME_FULL_ROOM);
            }
        }
        else if (currentPlayers >= 2)
        {
            // Apenas inicia a contagem se ainda não estiver a contar ou se a duração mudou
            if (!isCountingDown || countdownDuration != WAIT_TIME_FOR_SECOND_PLAYER)
            {
                SetStartTime(WAIT_TIME_FOR_SECOND_PLAYER);
            }
        }
        else
        {
            // Menos de 2 jogadores (Para a contagem).
            StopCountdown();
        }
    }

    // Inicia/Sincroniza a contagem regressiva (APENAS MasterClient)
    private void SetStartTime(float duration)
    {
        countdownDuration = duration;

        // Só define o StartTime se o timer ainda não estiver a correr ou se a duração for diferente
        if (!isCountingDown || 
            (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.StartTime) && 
            (double)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.StartTime] == 0) || // Se estiver parado (StartTime = 0)
            Mathf.Abs((float)(PhotonNetwork.Time - startTime - countdownDuration)) > 0.1f) // Ou se o tempo restante for muito diferente
        {
            Hashtable props = new Hashtable
            {
                { CustomRoomProperties.StartTime, PhotonNetwork.Time }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            isCountingDown = true;
            Debug.Log($"MasterClient iniciou/resetou timer. Duração: {duration}s. Tempo Photon: {PhotonNetwork.Time}");
        } else {
            Debug.Log($"MasterClient: Timer já ativo com duração correta ({duration}s), sem necessidade de reiniciar.");
        }
    }

    // Para a contagem regressiva (APENAS MasterClient)
    private void StopCountdown()
    {
        if (isCountingDown)
        {
            isCountingDown = false;
            // Define a propriedade como 'null' para parar a contagem em todos os clientes
            Hashtable props = new Hashtable { { CustomRoomProperties.StartTime, null } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            startTime = 0; // Resetar startTime
            remainingTime = 0f; // Resetar remainingTime

            Debug.Log("Contagem regressiva parada.");
        }
    }

    // --- INÍCIO DA PARTIDA (MASTER CLIENT) ---

    private void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        isCountingDown = false;

        // O MasterClient define a propriedade da sala para que todos os clientes saibam que o jogo começou
        Hashtable props = new Hashtable { { CustomRoomProperties.GameStarted, true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        startTime = 0; // Resetar startTime quando o jogo começa.
        remainingTime = 0f; // Resetar remainingTime
    }

    // --- LÓGICA DE TRANSIÇÃO PARA O JOGO (TODOS OS CLIENTES) ---

    private void GameStartLogic()
    {
        if (GameStartedAndPlayerCanMove) return;

        Debug.Log("Partida Iniciada! Removendo UI do Lobby e spawnando jogadores.");

        GameStartedAndPlayerCanMove = true;

        // 1. Desativa a tela de espera
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }

        // 2. Desativa a câmera do Lobby AGORA que o jogador vai dar spawn
        if (RoomManager.instance != null && RoomManager.instance.roomCam != null)
        {
            RoomManager.instance.roomCam.SetActive(false);
        }

        // 3. Permite que o RoomManager spawne o jogador local.
        if (RoomManager.instance != null)
        {
            RoomManager.instance.SetInitialRespawnCount(PhotonNetwork.LocalPlayer);
            RoomManager.instance.RespawnPlayer();
        }

        // 4. Fechea sala para novos jogadores
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }

        // Resetar o estado do lobby
        isCountingDown = false;
        startTime = 0;
        remainingTime = 0f;
    }

    // --- LÓGICA DA UI ---

    // Atualiza a lista de jogadores e o estado de espera
    private void UpdateLobbyUI()
    {
        if (!PhotonNetwork.InRoom) return;

        // CORREÇÃO DE ERRO: Garante que a referência playerListText existe
        if (playerListText == null) return;

        // 1. Atualiza a lista de jogadores
        string players = $"Jogadores na Sala ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS}):\n";
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
        {
            string nick = string.IsNullOrEmpty(p.NickName) ? $"Player {p.ActorNumber}" : p.NickName;
            players += $"- **{nick}** {(p.IsMasterClient ? "(Host)" : "")}\n";
        }
        playerListText.text = players;
    }

    // Atualiza o texto do timer
    private void UpdateCountdownUI(float time)
    {
        // CORREÇÃO DE ERRO: Garante que a referência countdownText existe
        if (countdownText == null) return;

        // Se a contagem parou ou o jogo começou, mostra uma mensagem estática
        // Verificar se o jogo já começou antes de exibir mensagens de espera
        bool gameAlreadyStarted = false;
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted))
        {
            gameAlreadyStarted = (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted];
        }

        if (gameAlreadyStarted)
        {
            // Se o jogo já começou, o UI do lobby não deve estar ativo, mas por segurança, limpa o texto.
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

        // Formata o tempo
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);

        // Exibe a mensagem de acordo com o estado
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
