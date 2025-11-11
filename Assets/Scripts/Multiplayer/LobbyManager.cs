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
    public const string StartTime = "st";        // Timestamp (double) de quando o timer começou
    public const string GameStarted = "gs";      // Sinaliza se o jogo começou (bool)
}

public class LobbyManager : MonoBehaviourPunCallbacks
{
    // --- Singleton Pattern ---
    public static LobbyManager instance;

    // --- NOVO FLAG PARA CONTROLE DE MOVIMENTO ---
    // Outros scripts (como o de movimento) podem ler este flag para saber se podem agir.
    public static bool GameStartedAndPlayerCanMove = false;

    // --- Configurações de Tempo ---
    private const int MAX_PLAYERS = 4;
    private const float WAIT_TIME_FOR_SECOND_PLAYER = 90f; // 90 segundos para 2+ jogadores
    private const float WAIT_TIME_FULL_ROOM = 5f;         // 5 segundos se a sala estiver cheia (4/4)

    // --- Variáveis de Sincronização e Estado ---
    private bool isCountingDown = false;
    private double startTime;             // O tempo real do Photon em que a contagem começou
    private float countdownDuration;      // Duração total da contagem regressiva (90s ou 5s)
    private float remainingTime;          // Tempo restante no countdown

    // --- Referências UI (Anexar no Inspector) ---
    [Header("UI Elements")]
    public GameObject lobbyPanel;         // O painel que contém toda a UI do lobby (ativar/desativar)
    public TMPro.TextMeshProUGUI countdownText;     // Texto para mostrar o tempo restante
    public TMPro.TextMeshProUGUI playerListText;    // Texto para mostrar a lista de jogadores

    private void Awake()
    {
        // Garante que haja apenas uma instância do LobbyManager
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;

        // **CORREÇÃO 1: Garante que o movimento esteja desativado por padrão ao carregar a cena**
        // Isto previne o spawn e movimento imediatos do Player 1 se o seu script de movimento estiver a verificar esta variável.
        GameStartedAndPlayerCanMove = false;
    }

    // --- ENTRADA NO LOBBY ---

    // Chamada pelo RoomManager após o jogador entrar na sala (OnJoinedRoom)
    public void OnRoomEntered()
    {
        // Ativa a tela de espera/lobby
        lobbyPanel.SetActive(true);

        // MasterClient verifica imediatamente as condições de início
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }

        // Garante que a UI seja inicializada
        UpdateLobbyUI();
    }

    // --- UPDATE LOOP (Lógica de Contagem) ---
    void Update()
    {
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
        remainingTime = Mathf.Max(0f, countdownDuration - (float)elapsed);

        // Atualiza a UI para todos os clientes
        UpdateCountdownUI(remainingTime);

        // APENAS o MasterClient decide o que fazer quando o tempo acaba
        if (PhotonNetwork.IsMasterClient && remainingTime <= 0f)
        {
            StartGame();
        }
    }

    // --- CALLBACKS DO PHOTON (Para MasterClient Controlar o Estado) ---

    // Chamado quando um novo jogador entra (o MasterClient deve reagir)
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        // O MasterClient chama CheckStartConditions, que apenas INICIA O TIMER se for necessário.
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }

        UpdateLobbyUI(); // Atualiza a lista de jogadores.
    }

    // Chamado quando um jogador sai (o MasterClient deve reagir)
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (PhotonNetwork.IsMasterClient)
        {
            CheckStartConditions();
        }
        UpdateLobbyUI(); // Atualiza a lista de jogadores para todos
    }

    // Chamado quando propriedades da sala são modificadas (Sincroniza o Timer/Início)
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        // 2. Sincronização do GameStarted (gs)
        // Se esta propriedade mudou, processamos primeiro o início do jogo.
        if (propertiesThatChanged.ContainsKey(CustomRoomProperties.GameStarted))
        {
            if ((bool)propertiesThatChanged[CustomRoomProperties.GameStarted])
            {
                // Todos os clientes que recebem esta propriedade iniciam o jogo
                GameStartLogic();

                // Uma vez que o jogo começou, ignoramos as atualizações de tempo abaixo
                return;
            }
        }

        // **CORREÇÃO 2: Verificação para evitar que o timer recomece após o jogo começar**
        // Só processamos a atualização do StartTime se o jogo AINDA NÃO começou.
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CustomRoomProperties.GameStarted) &&
            (bool)PhotonNetwork.CurrentRoom.CustomProperties[CustomRoomProperties.GameStarted])
        {
            // O jogo já começou, ignorar qualquer redefinição de tempo.
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
                Debug.Log($"Timer sincronizado. Duração: {countdownDuration}s. Início: {startTime}");
            }
            else
            {
                // Se o valor for null, significa que o MasterClient parou o timer
                isCountingDown = false;
                UpdateCountdownUI(0f); // Reseta a UI do tempo
            }
        }

        // Atualiza a UI para todos os clientes (lista de jogadores, etc.)
        UpdateLobbyUI();
    }


    // --- LÓGICA DE INÍCIO DE JOGO (MASTER CLIENT APENAS) ---

    // Verifica as condições de início e ajusta o timer (APENAS MasterClient)
    private void CheckStartConditions()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;

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
            // 90 segundos se o segundo jogador acabou de entrar e o timer não começou.
            if (!isCountingDown)
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

        Hashtable props = new Hashtable
        {
            // Define o tempo real do Photon para sincronizar
            { CustomRoomProperties.StartTime, PhotonNetwork.Time }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        isCountingDown = true;
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

            Debug.Log("Contagem regressiva parada.");
        }
    }

    // --- INÍCIO DA PARTIDA (MASTER CLIENT) ---

    private void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        isCountingDown = false;

        // 1. O MasterClient define a propriedade da sala para que todos os clientes saibam que o jogo começou
        // (Isto aciona o GameStartLogic() via OnRoomPropertiesUpdate para TODOS)
        Hashtable props = new Hashtable { { CustomRoomProperties.GameStarted, true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        // A chamada GameStartLogic() foi removida daqui, evitando a duplicação no Host.
    }

    // --- LÓGICA DE TRANSIÇÃO PARA O JOGO (TODOS OS CLIENTES) ---

    private void GameStartLogic()
    {
        // Esta verificação garante que não tentamos iniciar o jogo múltiplas vezes
        if (GameStartedAndPlayerCanMove) return;

        Debug.Log("Partida Iniciada! Removendo UI do Lobby e spawnando jogadores.");

        // ESTE É O PONTO CHAVE: Permite o movimento agora
        GameStartedAndPlayerCanMove = true;

        // 1. Desativa a tela de espera
        lobbyPanel.SetActive(false);

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

        // 4. Fecha a sala para novos jogadores
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }
    }

    // --- LÓGICA DA UI ---

    // Atualiza a lista de jogadores e o estado de espera
    private void UpdateLobbyUI()
    {
        if (!PhotonNetwork.InRoom) return;

        // 1. Atualiza a lista de jogadores
        if (playerListText != null)
        {
            string players = $"Jogadores na Sala ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS}):\n";
            foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
            {
                // Se o nickname estiver vazio, usa o ActorNumber como fallback
                string nick = string.IsNullOrEmpty(p.NickName) ? $"Player {p.ActorNumber}" : p.NickName;
                players += $"- **{nick}** {(p.IsMasterClient ? "(Host)" : "")}\n";
            }
            playerListText.text = players;
        }
    }

    // Atualiza o texto do timer
    private void UpdateCountdownUI(float time)
    {
        // Se a contagem parou ou o jogo começou, mostra uma mensagem estática
        if (!isCountingDown || time <= 0)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
            {
                countdownText.text = $"Aguardando 1° jogador...\n(Mínimo de 2 para começar)";
            }
            else if (PhotonNetwork.CurrentRoom.PlayerCount >= 2 && !isCountingDown)
            {
                // Mensagem caso o timer tenha sido parado (ex: alguém saiu e o count caiu para 1)
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
