using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using TMPro; // NECESSÁRIO para o componente de texto

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;

    public GameObject player;

    [Space]
    public Transform[] spawnPoints;

    [Space]
    public GameObject roomCam;

    private string nickName = "Nameless";

    [Space]
    public GameObject nameUI;
    public GameObject connectigUI;

    // UI do Timer: LIGUE este campo ao seu objeto TextMeshPro no Inspector!
    [Header("UI do Timer")]
    public TMP_Text countdownText;

    public string roomNameToJoin = "Noname";

    // --- Variáveis do Countdown ---
    private const string START_TIME_KEY = "StrtTme";     // Chave da propriedade da sala para o tempo de início
    private const int COUNTDOWN_DURATION = 90;          // Duração do countdown em segundos
    private const int MIN_PLAYERS_TO_START = 2;         // Número mínimo de jogadores para iniciar o countdown

    private float gameStartTime = 0f;                   // Tempo exato, sincronizado, em que o jogo deve começar
    private bool countdownStarted = false;              // Indica se o countdown foi iniciado
    private bool gameStarted = false;                   // Indica se a partida já começou
    // -----------------------------


    void Awake()
    {
        instance = this;

        // Oculta o timer ao iniciar (o timer só deve aparecer quando a contagem começa)
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    // --- Métodos de UI/Conexão Originais ---
    public void ChangeNickName(string _name)
    {
        nickName = _name;
    }

    public void JoinRoomButtonPressed()
    {
        Debug.Log("Connecting...");

        PhotonNetwork.JoinOrCreateRoom(roomNameToJoin, new Photon.Realtime.RoomOptions { MaxPlayers = 4 }, null);

        nameUI.SetActive(false);
        connectigUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log("Joined room! Player Count: " + PhotonNetwork.CurrentRoom.PlayerCount);

        roomCam.SetActive(false);

        RespawnPlayer();

        // 1. Master Client: verifica se o countdown deve começar (se já houver 2 players, incluindo ele)
        if (PhotonNetwork.IsMasterClient)
        {
            CheckAndStartCountdown();
        }
        else
        {
            // 2. Cliente Comum: verifica se o Master Client já iniciou o countdown
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(START_TIME_KEY))
            {
                gameStartTime = (float)(double)PhotonNetwork.CurrentRoom.CustomProperties[START_TIME_KEY];
                countdownStarted = true;

                // Ativa o timer na UI
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(true);
                }
            }
        }
    }

    public void RespawnPlayer()
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);

        // Assegura que os componentes PlayerSetup e Health existem
        if (_player.GetComponent<PlayerSetup>() != null)
        {
            _player.GetComponent<PlayerSetup>().IsLocalPlayer();
        }
        if (_player.GetComponent<Health>() != null)
        {
            _player.GetComponent<Health>().isLocalPlayer = true;
        }

        _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
        PhotonNetwork.LocalPlayer.NickName = nickName;
    }

    // --- Lógica do Countdown (Sincronização) ---

    // Chamado quando um novo jogador entra na sala
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        // Apenas o Master Client controla o início do countdown
        if (PhotonNetwork.IsMasterClient)
        {
            CheckAndStartCountdown();
        }
    }

    // Verifica a condição mínima de jogadores para iniciar
    private void CheckAndStartCountdown()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount >= MIN_PLAYERS_TO_START && !countdownStarted)
        {
            StartCountdown();
        }
    }

    // Inicia o countdown (apenas Master Client)
    private void StartCountdown()
    {
        countdownStarted = true;

        // Define o tempo de rede (sincronizado) em que o jogo deve começar
        double timeToStartGame = PhotonNetwork.Time + COUNTDOWN_DURATION;

        Hashtable customProps = new Hashtable
        {
            { START_TIME_KEY, timeToStartGame }
        };

        // Envia a propriedade para todos os clientes
        PhotonNetwork.CurrentRoom.SetCustomProperties(customProps);

        // Ativa o timer na UI
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }

        Debug.Log("Countdown iniciado!");
    }

    // Chamado em todos os clientes quando a propriedade do tempo é enviada
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        if (propertiesThatChanged.ContainsKey(START_TIME_KEY) && !gameStarted)
        {
            gameStartTime = (float)(double)propertiesThatChanged[START_TIME_KEY];
            countdownStarted = true;

            // Ativa o timer na UI
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
            }
        }
    }

    // --- Loop Principal do Jogo/Countdown ---
    void Update()
    {
        // Se o countdown está ativo e o jogo ainda não começou
        if (countdownStarted && !gameStarted)
        {
            // Tempo restante usando o tempo de rede sincronizado
            float timeLeft = gameStartTime - (float)PhotonNetwork.Time;

            if (timeLeft > 0f)
            {
                // Arredonda o tempo para cima para exibir
                int secondsLeft = Mathf.CeilToInt(timeLeft);

                // ATUALIZA O TEXTO DO TIMER
                if (countdownText != null)
                {
                    countdownText.text = "Início em: " + secondsLeft.ToString() + "s";
                }
            }
            else // Tempo Esgotado!
            {
                if (!gameStarted)
                {
                    gameStarted = true;
                    StartGameForAll();
                }
            }
        }
    }

    // Inicia o jogo para todos os jogadores
    private void StartGameForAll()
    {
        Debug.Log(" O JOGO VAI COMEÇAR AGORA!");

        // Desativa a UI de espera/conexão e o timer
        connectigUI.SetActive(false);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
            countdownText.text = "";
        }

        // Adicione aqui a lógica de início de jogo (habilitar controles, etc.)
    }
}