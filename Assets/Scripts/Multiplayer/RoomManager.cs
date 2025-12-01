using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine.SceneManagement;

// Permite acessar SCM.selectedCharacter diretamente (Assumindo que SCM é um script estático ou Singleton)
using static SCM; 

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
    public Transform[] spawnPoints; // Array com posições de spawn no mapa

    [Header("UI References")]
    [Tooltip("A câmara usada no lobby/espera (desativada ao começar)")]
    public GameObject roomCam; 
    [Tooltip("UI para inserir o nome (Menu Principal)")]
    public GameObject nameUI; 
    [Tooltip("UI de 'A Conectar...'")]
    public GameObject connectigUI; 

    [Header("Room Info")]
    public string mapName = "Noname"; 
    
    // --- Propriedade pública usada pelo GameChat.cs ---
    public bool IsNamePanelActive => nameUI != null && nameUI.activeSelf;

    void Awake()
    {
        // 1. Garante que haja apenas uma instância do RoomManager (Singleton)
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        // 2. Mantém o objeto vivo entre cenas (Menu -> Jogo)
        DontDestroyOnLoad(this.gameObject);
        
        Debug.Log("[RoomManager] Inicializado.");
    }

    public void ChangeNickName(string _name)
    {
        // Define o nickname global do Photon ANTES de tentar conectar
        PhotonNetwork.NickName = _name;
    }

    // --- FUNÇÕES DE CONEXÃO E SALA ---

    public void ConnectToMaster()
    {
        // 1. Inicia a conexão com o Photon Master Server
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("[RoomManager] Tentando conectar ao Photon Master Server...");

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
            // Se o nickname foi definido mas não está conectado, inicia a conexão
            ConnectToMaster();
        }
    }

    // Lógica interna para criar ou entrar
    private void JoinRoomLogic()
    {
        Debug.Log("[RoomManager] Conexão estabelecida. Tentando entrar/criar sala...");

        RoomOptions ro = new RoomOptions();
        ro.MaxPlayers = 4; 

        // Propriedades da sala
        ro.CustomRoomProperties = new Hashtable()
        {
            // Assumimos que a cena atual é a cena do menu/lobby com o RoomManager
            { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex }, 
            { "mapName", mapName }
        };

        ro.CustomRoomPropertiesForLobby = new[]
        {
            "mapSceneIndex",
            "mapName"
        };

        // Entra ou cria a sala
        PhotonNetwork.JoinOrCreateRoom(PlayerPrefs.GetString("RoomNameToJoin", "DefaultRoom"), ro, typedLobby: null);
    }

    // --- FUNÇÃO PARA SAIR (Retorna ao Menu) ---

    public void LeaveGameAndGoToMenu(string menuSceneName)
    {
        Debug.Log("[RoomManager] A sair do jogo e a voltar ao menu...");

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
            // Fallback se não estiver na sala/conectado
            SceneManager.LoadScene(menuSceneName);
            // Destrói o Singleton aqui para resetar o estado.
            if (instance == this)
            {
                instance = null;
                Destroy(this.gameObject);
            }
        }
    }

    // --- CALLBACKS DO PHOTON ---

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log("[RoomManager] Conectado ao Master Server! Tentando entrar em sala...");
        JoinRoomLogic();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.LogError($"[RoomManager] Desconectado. Causa: {cause}");
        
        // Garante que o feedback da UI volta ao estado inicial
        if (connectigUI != null) connectigUI.SetActive(false);
        if (nameUI != null) nameUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        Debug.Log($"[RoomManager] Entrou na sala '{PhotonNetwork.CurrentRoom.Name}' com sucesso!");

        if (connectigUI != null) connectigUI.SetActive(false);
        
        // Define as vidas iniciais assim que entra (Apenas define a propriedade)
        SetInitialRespawnCount(PhotonNetwork.LocalPlayer);
        
        // Avisa o LobbyManager para mostrar a UI de espera e iniciar a contagem
        if (LobbyManager.instance != null)
        {
            LobbyManager.instance.OnRoomEntered();
        }
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        Debug.Log("[RoomManager] Saiu da sala (OnLeftRoom). Carregando Menu...");

        // Carrega a cena do menu
        if (!string.IsNullOrEmpty(sceneToLoadOnLeave))
        {
            // É essencial destruir ANTES de carregar a cena para evitar 
            // que a nova cena tente aceder a este objeto durante o carregamento
            if (instance == this)
            {
                // ** CORREÇÃO DE SEGURANÇA DO SINGLETON **
                instance = null; // Limpa a referência estática
                Destroy(this.gameObject); // Destrói o objeto
            }

            SceneManager.LoadScene(sceneToLoadOnLeave);
            sceneToLoadOnLeave = "";
        }
    }

    // --- LÓGICA DE SPAWN E RESPAWN ---

    public void SetInitialRespawnCount(Player player)
    {
        // Define o contador de respawns no Photon.Player custom properties
        if (!player.CustomProperties.ContainsKey(RESPAWN_COUNT_KEY))
        {
            Hashtable props = new Hashtable();
            props.Add(RESPAWN_COUNT_KEY, MAX_RESPAWNS);
            player.SetCustomProperties(props);
            Debug.Log($"[RoomManager] Respawns definidos para {player.NickName}: {MAX_RESPAWNS}");
        }
    }

    // Função chamada pelo LobbyManager para criar o boneco (ou por Health.cs para respawn)
    public void RespawnPlayer()
    {
        // 1. Verifica vidas restantes
        int respawnsLeft = GetRespawnCount(PhotonNetwork.LocalPlayer);

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[RoomManager] ERRO: Array 'spawnPoints' está vazio!");
            return;
        }
        
        // ** INTEGRANDO A SELEÇÃO DE PERSONAGEM **
        // Usamos SCM.selectedCharacter, assumindo que foi definido no menu
        string characterToSpawnName = SCM.selectedCharacter;

        if (string.IsNullOrEmpty(characterToSpawnName) || characterToSpawnName == "None")
        {
            Debug.LogError("[RoomManager] ERRO: Personagem não selecionado. Não é possível fazer o spawn.");
            // Fallback para um nome padrão se falhar
            characterToSpawnName = "DefaultPlayerPrefab"; 
        }
        
        // Se tiver vidas (incluindo o spawn inicial)
        if (respawnsLeft >= 0)
        {
            // --- Lógica de seleção de Ponto de Spawn ---
            int playerIndex = GetPlayerIndex(PhotonNetwork.LocalPlayer);
            // Seleciona um ponto de spawn baseado no índice do jogador (cicla)
            int spawnIndex = playerIndex % spawnPoints.Length; 

            Transform spawnPoint = spawnPoints[spawnIndex];

            // Instancia o jogador na rede
            GameObject _player = PhotonNetwork.Instantiate(characterToSpawnName, spawnPoint.position, Quaternion.identity);

            // Configurações locais (Ativa Movement/Combat no jogador local)
            _player.GetComponent<PlayerSetup>()?.IsLocalPlayer();

            Debug.Log($"[RoomManager] Spawn de '{characterToSpawnName}' realizado no ponto {spawnIndex}. Vidas restantes: {respawnsLeft}");
        }
        else
        {
            Debug.Log("[RoomManager] Game Over: Limite de respawns atingido.");
            // Lógica de Game Over / Espectador
        }
    }

    // Chamado pelo Script de Vida quando alguém morre (Executado SOMENTE no MasterClient)
    public void OnPlayerDied(Player playerWhoDied)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Garante autoridade do servidor

        int currentRespawnCount = GetRespawnCount(playerWhoDied);

        if (currentRespawnCount > 0)
        {
            currentRespawnCount--;
            // Atualiza a propriedade sincronizada
            Hashtable props = new Hashtable { { RESPAWN_COUNT_KEY, currentRespawnCount } };
            playerWhoDied.SetCustomProperties(props);
            Debug.Log($"[Server] {playerWhoDied.NickName} morreu. Restam: {currentRespawnCount}");
        }
    }

    // --- UTILITÁRIOS ---

    private int GetRespawnCount(Player player)
    {
        // Obtém a contagem de respawns das propriedades customizadas
        if (player.CustomProperties.TryGetValue(RESPAWN_COUNT_KEY, out object count))
        {
            return (int)count;
        }
        // Retorna o valor máximo se a propriedade ainda não foi definida (primeiro spawn/erro)
        return MAX_RESPAWNS;
    }

    private int GetPlayerIndex(Player player)
    {
        // Obtém o índice do jogador na lista global de jogadores
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == player) return i;
        }
        return 0; // Fallback
    }
}
