using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;

    // O jogador tem o SPAWN INICIAL + 3 RESPAWNS (total de 4 vidas/chances)
    private const int MAX_RESPAWNS = 2;

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

    private string nickName = "Nameless";

    public string mapName = "Noname";

    void Awake()
    {
        // Garante que haja apenas uma instância do RoomManager
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        // Mantém o objeto vivo entre cenas
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
            nameUI.SetActive(false);
            connectigUI.SetActive(true);
        }
        else
        {
            // Se já estiver conectado, pula para tentar entrar na sala
            JoinRoomLogic();
        }
    }

    // Chamado ao pressionar o botão de Juntar Sala
    public void JoinRoomButtonPressed()
    {
        // Se já estiver conectado ao Master, pode prosseguir
        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinRoomLogic();
        }
        else
        {
            // Se não estiver, inicia a conexão.
            ConnectToMaster();
        }
    }

    // Lógica para criar ou entrar numa sala
    private void JoinRoomLogic()
    {
        Debug.Log("Conexão estabelecida. Tentando entrar/criar sala...");

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

        // Entra ou cria a sala com as opções definidas
        PhotonNetwork.JoinOrCreateRoom(roomName: PlayerPrefs.GetString(key: "RoomNameToJoin"), ro, typedLobby: null);
    }

    // --- CALLBACKS DO PHOTON ---

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log("Conectado ao Master Server! Lobby automático.");
        // Tenta entrar na sala imediatamente após conectar
        JoinRoomLogic();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.LogError($"Desconectado. Causa: {cause}");
        connectigUI.SetActive(false);
        nameUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log("Joined room!");

        roomCam.SetActive(false);
        connectigUI.SetActive(false); // Esconde a tela de connecting

        // Define a contagem inicial de respawn (se ainda não estiver definida)
        SetInitialRespawnCount(PhotonNetwork.LocalPlayer);

        RespawnPlayer();
    }

    // --- LÓGICA DE RESPAWN E MORTE ---

    public void SetInitialRespawnCount(Player player)
    {
        // Define o número inicial de respawns apenas se a propriedade não existir
        if (!player.CustomProperties.ContainsKey(RESPAWN_COUNT_KEY))
        {
            Hashtable props = new Hashtable();
            // Define 3 respawns restantes (além do spawn inicial)
            props.Add(RESPAWN_COUNT_KEY, MAX_RESPAWNS);
            player.SetCustomProperties(props);
            Debug.Log($"Jogador {player.NickName} inicializado com {MAX_RESPAWNS} respawns.");
        }
    }

    public void RespawnPlayer()
    {
        // Chama GetRespawnCount, que agora garante o valor MAX_RESPAWNS no primeiro spawn.
        int respawnsLeft = GetRespawnCount(PhotonNetwork.LocalPlayer);

        // O jogador pode dar respawn se a contagem for MAX_RESPAWNS (primeiro spawn)
        // OU se o valor sincronizado for maior que 0 (respawns seguintes).
        if (respawnsLeft > 0)
        {
            Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

            GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);

            _player.GetComponent<PlayerSetup>().IsLocalPlayer();
            _player.GetComponent<Health>().isLocalPlayer = true;

            _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
            PhotonNetwork.LocalPlayer.NickName = nickName;

            Debug.Log($"Respawn realizado para {PhotonNetwork.LocalPlayer.NickName}. Respawn(s) restante(s) antes da próxima morte: {respawnsLeft}");
        }
        else
        {
            // O jogador atingiu o limite de respawns
            Debug.Log($"LIMITE DE RESPAWN ATINGIDO! Jogador {PhotonNetwork.LocalPlayer.NickName} não pode mais dar respawn.");
        }
    }

    // Chamado pelo script Health.cs (no cliente que morreu)
    public void OnPlayerDied(Player playerWhoDied)
    {
        // APENAS o MasterClient deve manipular e sincronizar a contagem de respawns
        if (!PhotonNetwork.IsMasterClient) return;

        int currentRespawnCount = GetRespawnCount(playerWhoDied);

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

    // CORREÇÃO: Garante que o valor MAX_RESPAWNS é retornado se a propriedade ainda não estiver sincronizada
    private int GetRespawnCount(Player player)
    {
        if (player.CustomProperties.TryGetValue(RESPAWN_COUNT_KEY, out object count))
        {
            return (int)count;
        }
        // Se a propriedade ainda não foi definida (o que pode ocorrer no primeiro frame devido ao tempo de rede),
        // retorna o valor máximo, permitindo o spawn inicial.
        return MAX_RESPAWNS;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        Debug.LogFormat("OnPlayerLeftRoom() {0}", otherPlayer.NickName);
    }
}