using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomManager : MonoBehaviourPunCallbacks
{
    // --- Singleton ---
    public static RoomManager instance;

    // --- Constantes ---
    // O jogador tem o SPAWN INICIAL + MAX_RESPAWNS (total de chances = MAX_RESPAWNS + 1)
    private const int MAX_RESPAWNS = 2; // Significa 1 spawn inicial + 2 respawns = 3 vidas/chances
    // Chave da Propriedade Personalizada para rastrear respawns restantes
    private const string RESPAWN_COUNT_KEY = "RespawnCount";
    private const string MAIN_MENU_SCENE_NAME = "MainMenu"; // Para evitar hardcode em duas funções

    // --- Referências do Inspector ---
    public GameObject player;
    [Space]
    public Transform[] spawnPoints;
    [Space]
    public GameObject roomCam;
    [Space]
    public GameObject nameUI;
    public GameObject connectigUI;

    // --- Variáveis de Estado ---
    public bool IsNamePanelActive => nameUI.activeSelf;
    private string nickName = "Nameless";
    public string mapName = "Noname";

    // Flag para saber se estamos a voltar ao menu (essencial para o OnDisconnected)
    private bool returningToMenu = false;

    // --- Inicialização (Singleton e Persistência) ---

    void Awake()
    {
        // Garante que haja apenas uma instância do RoomManager (Singleton)
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

    // --- Funções de Conexão e Sala ---

    public void ConnectToMaster()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Tentando conectar ao Photon Master Server...");

            // 1. Esconde as UIs antes de conectar
            if (nameUI != null) nameUI.SetActive(false);
            if (connectigUI != null) connectigUI.SetActive(true);

            PhotonNetwork.ConnectUsingSettings();
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
        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinRoomLogic();
        }
        else
        {
            ConnectToMaster();
        }
    }

    // Função para ser chamada pelo BackButton (no jogo)
    public void GoToMainMenu()
    {
        // 1. Ativa a flag para sabermos que estamos a voltar ao menu
        returningToMenu = true;

        // 2. Tenta desconectar, se estivermos conectados
        if (PhotonNetwork.IsConnected)
        {
            // O OnDisconnected será chamado a seguir, onde o carregamento da cena ocorrerá
            PhotonNetwork.Disconnect();
        }
        // 3. Se não estivermos conectados, destrói e carrega imediatamente
        else
        {
            Destroy(this.gameObject);
            SceneManager.LoadScene(MAIN_MENU_SCENE_NAME);
        }
    }

    // Lógica para criar ou entrar numa sala
    private void JoinRoomLogic()
    {
        Debug.Log("Conexão estabelecida. Tentando entrar/criar sala...");

        RoomOptions ro = new RoomOptions
        {
            MaxPlayers = 4, // Limita a sala a 4 jogadores
            CustomRoomProperties = new Hashtable()
            {
                { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
                { "mapName", mapName }
            },
            CustomRoomPropertiesForLobby = new[]
            {
                "mapSceneIndex",
                "mapName"
            }
        };

        // Entra ou cria a sala com as opções definidas
        PhotonNetwork.JoinOrCreateRoom(PlayerPrefs.GetString("RoomNameToJoin"), ro, typedLobby: null);
    }

    // --- Callbacks do Photon ---

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log("Conectado ao Master Server! Lobby automático.");
        JoinRoomLogic();
    }

    // O Callback CHAVE para o retorno ao menu
    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.LogError($"Desconectado. Causa: {cause}");

        // Se a desconexão foi porque o utilizador clicou em "Voltar"
        if (returningToMenu)
        {
            // Garante que o RoomManager e as suas referências não persistem
            Destroy(this.gameObject);

            // Carrega a cena do menu principal
            SceneManager.LoadScene(MAIN_MENU_SCENE_NAME);
        }
        // Senão, foi uma desconexão inesperada (ex: falha de rede)
        else
        {
            // Volta para a UI de introdução
            if (connectigUI != null) connectigUI.SetActive(false);
            if (nameUI != null) nameUI.SetActive(true);
        }

        // Reset à flag após processar a desconexão
        returningToMenu = false;
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log("Joined room!");

        if (connectigUI != null) connectigUI.SetActive(false); // Esconde a tela de connecting

        // Chama o LobbyManager para iniciar a lógica de espera (UI do Lobby)
        if (LobbyManager.instance != null)
        {
            LobbyManager.instance.OnRoomEntered();
        }
    }

    // --- Lógica de Respawn e Morte ---

    public void SetInitialRespawnCount(Player player)
    {
        // Define o número inicial de respawns apenas se a propriedade não existir
        if (!player.CustomProperties.ContainsKey(RESPAWN_COUNT_KEY))
        {
            Hashtable props = new Hashtable
            {
                // Define 2 respawns restantes (além do spawn inicial)
                { RESPAWN_COUNT_KEY, MAX_RESPAWNS }
            };
            player.SetCustomProperties(props);
            Debug.Log($"Jogador {player.NickName} inicializado com {MAX_RESPAWNS} respawns restantes.");
        }
    }

    public void RespawnPlayer()
    {
        int respawnsLeft = GetRespawnCount(PhotonNetwork.LocalPlayer);

        // Permite o spawn se houver respawns restantes (incluindo o spawn inicial)
        if (respawnsLeft >= 0)
        {
            // Garante que existe pelo menos um ponto de spawn
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("Os pontos de spawn não estão definidos!");
                return;
            }

            Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

            // Instancia o prefab do jogador na rede
            GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);

            // Configurações do jogador local (será feito por RPC no código do jogador)
            _player.GetComponent<PlayerSetup>().IsLocalPlayer();

            // Define e sincroniza o Nickname (RPC para todos)
            _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
            PhotonNetwork.LocalPlayer.NickName = nickName;

            Debug.Log($"Respawn realizado para {PhotonNetwork.LocalPlayer.NickName}. Respawn(s) restante(s) antes da próxima morte: {respawnsLeft}");
        }
        else
        {
            Debug.Log($"LIMITE DE RESPAWN ATINGIDO! Jogador {PhotonNetwork.LocalPlayer.NickName} não pode mais dar respawn.");
        }
    }

    // Chamado pelo script Health.cs (no cliente que morreu)
    public void OnPlayerDied(Player playerWhoDied)
    {
        // APENAS o MasterClient deve manipular a contagem de respawns
        if (!PhotonNetwork.IsMasterClient) return;

        int currentRespawnCount = GetRespawnCount(playerWhoDied);

        // Decrementamos apenas se o jogador tiver respawns restantes
        if (currentRespawnCount > 0)
        {
            currentRespawnCount--;

            Hashtable props = new Hashtable
            {
                { RESPAWN_COUNT_KEY, currentRespawnCount }
            };
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
        // Se a propriedade ainda não foi definida, retorna o valor máximo (permite o spawn inicial)
        return MAX_RESPAWNS;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        Debug.LogFormat("OnPlayerLeftRoom() {0}", otherPlayer.NickName);
    }
}
