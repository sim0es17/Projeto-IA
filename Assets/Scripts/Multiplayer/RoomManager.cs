using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;

    // O jogador tem o SPAWN INICIAL + MAX_RESPAWNS (total de chances = MAX_RESPAWNS + 1)
    private const int MAX_RESPAWNS = 2; // Significa 1 spawn inicial + 2 respawns = 3 vidas/chances

    // Chave da Propriedade Personalizada para rastrear respawns restantes
    private const string RESPAWN_COUNT_KEY = "RespawnCount";

    public GameObject player;

    [Space]
    public Transform[] spawnPoints;

    [Space]
    public GameObject roomCam;

    [Space]
    public GameObject nameUI;
    public GameObject connectigUI;

    public bool IsNamePanelActive => nameUI.activeSelf;

    private string nickName = "Nameless";

    public string mapName = "Noname";

    // <-- ADICIONADO
    private bool returningToMenu = false; // Flag para saber se estamos a voltar ao menu

    void Awake()
    {
        // Garante que haja apenas uma inst ncia do RoomManager
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        // Mant m o objeto vivo entre cenas
        DontDestroyOnLoad(this.gameObject);
    }

    public void ChangeNickName(string _name)
    {
        nickName = _name;
    }

    // --- FUN  ES DE CONEX O E SALA ---

    public void ConnectToMaster()
    {
        // 1. Inicia a conex o com o Photon Master Server
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("Tentando conectar ao Photon Master Server...");
            nameUI.SetActive(false); // Esconde a UI do nome
            connectigUI.SetActive(true);
        }
        else
        {
            // Se j  estiver conectado, pula para tentar entrar na sala
            JoinRoomLogic();
        }
    }

    // Chamado ao pressionar o bot o de Juntar Sala
    public void JoinRoomButtonPressed()
    {
        // Se j  estiver conectado ao Master, pode prosseguir
        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinRoomLogic();
        }
        else
        {
            // Se n o estiver, inicia a conex o.
            ConnectToMaster();
        }
    }

    // <-- ADICIONADO (Fun  o completa)
    // Fun  o para ser chamada pelo BackButton
    public void GoToMainMenu()
    {
        // 1. Ativa a flag para sabermos que estamos a voltar ao menu
        returningToMenu = true;

        // 2. Tenta desconectar, se estivermos conectados
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        // 3. Se n o estivermos conectados, podemos simplesmente destruir e carregar
        else
        {
            Destroy(this.gameObject);
            SceneManager.LoadScene("MainMenu");
        }
    }

    // L gica para criar ou entrar numa sala
    private void JoinRoomLogic()
    {
        Debug.Log("Conex o estabelecida. Tentando entrar/criar sala...");

        RoomOptions ro = new RoomOptions();

        // Limita a sala a 4 jogadores
        ro.MaxPlayers = 4;

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

        // Entra ou cria a sala com as op  es definidas
        PhotonNetwork.JoinOrCreateRoom(roomName: PlayerPrefs.GetString(key: "RoomNameToJoin"), ro, typedLobby: null);
    }

    // --- CALLBACKS DO PHOTON ---

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log("Conectado ao Master Server! Lobby autom tico.");
        // Tenta entrar na sala imediatamente ap s conectar
        JoinRoomLogic();
    }

    // <-- MODIFICADO (Fun  o inteira)
    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.LogError($"Desconectado. Causa: {cause}");

        // SE a desconex o foi porque o utilizador clicou em "Voltar"
        if (returningToMenu)
        {
            // 4. Agora sim, destr i o RoomManager e carrega o menu
            Destroy(this.gameObject);
            SceneManager.LoadScene("MainMenu");
        }
        // Sen o, foi uma desconex o inesperada (ex: falha de rede)
        else
        {
            connectigUI.SetActive(false);
            nameUI.SetActive(true);
        }
    }

    // **FUN  O CHAVE:** N o d  spawn diretamente; apenas passa o controle ao LobbyManager.
    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log("Joined room!");

        connectigUI.SetActive(false); // Esconde a tela de connecting

        // 1. Chama o LobbyManager para iniciar a l gica de espera (UI do Lobby)
        if (LobbyManager.instance != null)
        {
            LobbyManager.instance.OnRoomEntered();
        }

        // 2. O Spawn do jogador foi REMOVIDO daqui. Ele   chamado APENAS pelo LobbyManager.GameStartLogic().
        // RespawnPlayer(); // (Corretamente comentado/removido)
    }

    // --- L GICA DE RESPAWN E MORTE ---

    public void SetInitialRespawnCount(Player player)
    {
        // Define o n mero inicial de respawns apenas se a propriedade n o existir
        if (!player.CustomProperties.ContainsKey(RESPAWN_COUNT_KEY))
        {
            Hashtable props = new Hashtable();
            // Define 2 respawns restantes (al m do spawn inicial)
            props.Add(RESPAWN_COUNT_KEY, MAX_RESPAWNS);
            player.SetCustomProperties(props);
            Debug.Log($"Jogador {player.NickName} inicializado com {MAX_RESPAWNS} respawns restantes.");
        }
    }

    public void RespawnPlayer()
    {
        // Obt m a contagem de respawns restantes.
        // O valor MAX_RESPAWNS (2)   retornado se esta for a primeira vez.
        int respawnsLeft = GetRespawnCount(PhotonNetwork.LocalPlayer);

        // O jogador pode dar spawn se a contagem for MAX_RESPAWNS (primeiro spawn)
        // OU se o valor sincronizado for maior ou igual a 0 (respawns seguintes).
        // Se respawnsLeft for MAX_RESPAWNS (2), este   o primeiro spawn.
        // Se respawnsLeft for 1 ou 0, s o os respawns seguintes.

        // CORRE  O: Usar respawnsLeft >= 0 para incluir o spawn inicial e todos os respawns seguintes
        // at  esgotar a contagem. MAX_RESPAWNS (2) + 1 vida   a contagem total.
        if (respawnsLeft >= 0)
        {
            Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

            GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);

            // Configura  es do jogador local
            _player.GetComponent<PlayerSetup>().IsLocalPlayer();
            _player.GetComponent<Health>().isLocalPlayer = true;

            // Define e sincroniza o Nickname
            _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
            PhotonNetwork.LocalPlayer.NickName = nickName;

            Debug.Log($"Respawn realizado para {PhotonNetwork.LocalPlayer.NickName}. Respawn(s) restante(s) antes da pr xima morte: {respawnsLeft}");
        }
        else
        {
            // O jogador atingiu o limite de respawns
            Debug.Log($"LIMITE DE RESPAWN ATINGIDO! Jogador {PhotonNetwork.LocalPlayer.NickName} n o pode mais dar respawn.");
        }
    }

    // Chamado pelo script Health.cs (no cliente que morreu)
    public void OnPlayerDied(Player playerWhoDied)
    {
        // APENAS o MasterClient deve manipular e sincronizar a contagem de respawns
        if (!PhotonNetwork.IsMasterClient) return;

        int currentRespawnCount = GetRespawnCount(playerWhoDied);

        // S  decrementamos se o jogador tiver respawns restantes (currentRespawnCount > 0)
        if (currentRespawnCount > 0)
        {
            // Decrementa a contagem de respawns
            currentRespawnCount--;

            Hashtable props = new Hashtable();
            props.Add(RESPAWN_COUNT_KEY, currentRespawnCount);
            playerWhoDied.SetCustomProperties(props);

            Debug.Log($"[MasterClient] Jogador {playerWhoDied.NickName} morreu. Restam {currentRespawnCount} respawn(s).");
        }
    }

    private int GetRespawnCount(Player player)
    {
        if (player.CustomProperties.TryGetValue(RESPAWN_COUNT_KEY, out object count))
        {
            return (int)count;
        }
        // Se a propriedade ainda n o foi definida, retorna o valor m ximo (permite o spawn inicial)
        return MAX_RESPAWNS;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        Debug.LogFormat("OnPlayerLeftRoom() {0}", otherPlayer.NickName);
    }

    // Os callbacks OnEnable, OnDisable, e OnSceneLoaded foram removidos
}