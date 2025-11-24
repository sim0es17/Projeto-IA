using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomManager : MonoBehaviourPunCallbacks
{
    // --- Singleton Pattern ---
    public static RoomManager instance;

    // --- Configurações de Jogo ---
    // O jogador tem o SPAWN INICIAL + MAX_RESPAWNS (total de chances = MAX_RESPAWNS + 1)
    private const int MAX_RESPAWNS = 2; // 1 spawn inicial + 2 respawns = 3 vidas totais
    private const string RESPAWN_COUNT_KEY = "RespawnCount"; // Chave para sincronização de rede

    // Variável interna para transição de cenas
    private string sceneToLoadOnLeave = "";

    [Header("Player and Spawn")]
    public GameObject player; // O Prefab do jogador (deve estar na pasta Resources)
    public Transform[] spawnPoints; // Array com posições de spawn no mapa

    [Header("UI References")]
    public GameObject roomCam;     // A câmara usada no lobby/espera (desativada ao começar)
    public GameObject nameUI;      // UI para inserir o nome (Menu Principal)
    public GameObject connectigUI; // UI de 'A Conectar...'

    [Header("Room Info")]
    public string mapName = "Noname"; 
    private string nickName = "Nameless";

    // --- Propriedade pública usada pelo GameChat.cs ---
    // Retorna true se o menu de inserir nome estiver visível (bloqueia o chat)
    public bool IsNamePanelActive => nameUI != null && nameUI.activeSelf;

    void Awake()
    {
        // Garante que haja apenas uma instância do RoomManager (Singleton)
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        // Mantém o objeto vivo entre cenas (Menu -> Jogo)
        DontDestroyOnLoad(this.gameObject);
    }

    public void ChangeNickName(string _name)
    {
        nickName = _name;
    }

    // --- FUNÇÕES DE CONEXÃO E SALA ---

    public void ConnectToMaster()
    {
        // 1. Inicia a conexão com o Photon Master Server
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("Tentando conectar ao Photon Master Server...");

            // UI Feedback
            if (nameUI != null) nameUI.SetActive(false);
            if (connectigUI != null) connectigUI.SetActive(true);
        }
        else
        {
            // Se já estiver conectado, pula para tentar entrar na sala
            JoinRoomLogic();
        }
    }

    // Chamado pelo botão "Join Room" na UI
    public void JoinRoomButtonPressed()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinRoomLogic();
        }
        else
        {
            ConnectToMaster();
        }
    }

    // Lógica interna para criar ou entrar
    private void JoinRoomLogic()
    {
        Debug.Log("Conexão estabelecida. Tentando entrar/criar sala...");

        RoomOptions ro = new RoomOptions();
        ro.MaxPlayers = 4; // Limite de jogadores

        // Propriedades da sala (sincroniza qual mapa está a ser jogado)
        ro.CustomRoomProperties = new Hashtable()
        {
            { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
            { "mapName", mapName }
        };

        ro.CustomRoomPropertiesForLobby = new[]
        {
            "mapSceneIndex",
            "mapName"
        };

        // Entra ou cria a sala baseada no nome guardado ou padrão
        PhotonNetwork.JoinOrCreateRoom(PlayerPrefs.GetString("RoomNameToJoin", "DefaultRoom"), ro, typedLobby: null);
    }

    // --- FUNÇÃO PARA SAIR (Retorna ao Menu) ---

    public void LeaveGameAndGoToMenu(string menuSceneName)
    {
        Debug.Log("A sair do jogo e a voltar ao menu...");

        // 1. Reset ao tempo (caso estivesse em pausa)
        Time.timeScale = 1f;

        // 2. Guarda a cena para carregar DEPOIS de desconectar da sala
        sceneToLoadOnLeave = menuSceneName;

        // 3. Photon Leave (Assíncrono -> vai chamar OnLeftRoom)
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            // Fallback se não estiver na sala
            SceneManager.LoadScene(menuSceneName);
            Destroy(this.gameObject);
        }
    }

    // --- CALLBACKS DO PHOTON ---

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log("Conectado ao Master Server! Tentando entrar em sala...");
        JoinRoomLogic();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.LogError($"Desconectado. Causa: {cause}");
        if (connectigUI != null) connectigUI.SetActive(false);
        if (nameUI != null) nameUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        Debug.Log("Entrou na sala com sucesso!");

        if (connectigUI != null) connectigUI.SetActive(false);

        // Define as vidas iniciais assim que entra
        SetInitialRespawnCount(PhotonNetwork.LocalPlayer);
        
        // Avisa o LobbyManager para mostrar a UI de espera
        if (LobbyManager.instance != null)
        {
            LobbyManager.instance.OnRoomEntered();
        }
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        Debug.Log("Saiu da sala (OnLeftRoom). Carregando Menu...");

        // Carrega a cena do menu
        if (!string.IsNullOrEmpty(sceneToLoadOnLeave))
        {
            SceneManager.LoadScene(sceneToLoadOnLeave);
            
            // Destrói este Singleton para resetar o estado ao voltar ao menu
            if (instance == this)
            {
                Destroy(this.gameObject);
            }
            sceneToLoadOnLeave = "";
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        Debug.Log($"{otherPlayer.NickName} saiu da sala.");
        // A lista de players atualiza automaticamente
    }

    // --- LÓGICA DE SPAWN E RESPAWN ---

    public void SetInitialRespawnCount(Player player)
    {
        // Só define se ainda não tiver a propriedade (para não resetar se reconectar)
        if (!player.CustomProperties.ContainsKey(RESPAWN_COUNT_KEY))
        {
            Hashtable props = new Hashtable();
            props.Add(RESPAWN_COUNT_KEY, MAX_RESPAWNS);
            player.SetCustomProperties(props);
            Debug.Log($"Respawns definidos para {player.NickName}: {MAX_RESPAWNS}");
        }
    }

    // Função chamada pelo LobbyManager para criar o boneco
    public void RespawnPlayer()
    {
        // Verifica vidas restantes
        int respawnsLeft = GetRespawnCount(PhotonNetwork.LocalPlayer);

        // Verificação de segurança dos pontos de spawn
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("ERRO: Array 'spawnPoints' está vazio no RoomManager!");
            return;
        }

        // Se tiver vidas (ou spawn inicial)
        if (respawnsLeft >= 0 && player != null)
        {
            // --- Lógica de seleção de Ponto de Spawn ---
            // Tenta pegar um ponto baseado no ID do jogador para evitar sobreposição inicial
            int playerIndex = GetPlayerIndex(PhotonNetwork.LocalPlayer);
            int spawnIndex = playerIndex % spawnPoints.Length; // Garante que não sai do limite do array

            Transform spawnPoint = spawnPoints[spawnIndex];

            // Instancia o jogador na rede
            GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);

            // Configurações locais
            _player.GetComponent<PlayerSetup>()?.IsLocalPlayer();

            // Sincroniza o Nickname no boneco
            if (_player.GetComponent<PhotonView>() != null)
            {
                _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
                PhotonNetwork.LocalPlayer.NickName = nickName;
            }

            Debug.Log($"Spawn realizado no ponto {spawnIndex}. Vidas restantes: {respawnsLeft}");
        }
        else
        {
            Debug.Log("Game Over: Limite de respawns atingido.");
            // Aqui podes chamar uma UI de espectador ou Game Over
        }
    }

    // Chamado pelo Script de Vida quando alguém morre (Executado no MasterClient)
    public void OnPlayerDied(Player playerWhoDied)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int currentRespawnCount = GetRespawnCount(playerWhoDied);

        if (currentRespawnCount > 0)
        {
            currentRespawnCount--;
            Hashtable props = new Hashtable { { RESPAWN_COUNT_KEY, currentRespawnCount } };
            playerWhoDied.SetCustomProperties(props);
            Debug.Log($"[Server] {playerWhoDied.NickName} morreu. Restam: {currentRespawnCount}");
        }
    }

    // --- UTILITÁRIOS ---

    private int GetRespawnCount(Player player)
    {
        if (player.CustomProperties.TryGetValue(RESPAWN_COUNT_KEY, out object count))
        {
            return (int)count;
        }
        return MAX_RESPAWNS;
    }

    private int GetPlayerIndex(Player player)
    {
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == player) return i;
        }
        return 0;
    }
}
