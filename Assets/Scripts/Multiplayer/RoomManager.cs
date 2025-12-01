using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using Photon.Pun.UtilityScripts;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;

    // --- Configurações de Jogo ---
    // 1 Vida Inicial + 2 Respawns = 3 Vidas Totais
    private const int MAX_RESPAWNS = 2;
    private const string RESPAWN_COUNT_KEY = "RespawnCount";
    private string sceneToLoadOnLeave = "";

    [Header("Player and Spawn")]
    public GameObject player;
    public Transform[] spawnPoints;

    [Header("UI References")]
    public GameObject roomCam;      // Câmara do Lobby/Espectador
    public GameObject nameUI;       // Menu de Nome
    public GameObject connectigUI;  // Texto "Connecting..."

    // ARRASTA O TEU HUDCANVAS PARA AQUI
    public MultiplayerEndScreen endScreen;

    [Header("Room Info")]
    public string mapName = "Noname";
    private string nickName = "Nameless";

    public bool IsNamePanelActive => nameUI != null && nameUI.activeSelf;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    public void ChangeNickName(string _name) { nickName = _name; }

    // --- CONEXÃO ---
    public void ConnectToMaster()
    {
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            if (nameUI != null) nameUI.SetActive(false);
            if (connectigUI != null) connectigUI.SetActive(true);
        }
        else JoinRoomLogic();
    }

    public void JoinRoomButtonPressed()
    {
        if (PhotonNetwork.IsConnectedAndReady) JoinRoomLogic();
        else ConnectToMaster();
    }

    private void JoinRoomLogic()
    {
        RoomOptions ro = new RoomOptions();
        ro.MaxPlayers = 4;

        ro.CustomRoomProperties = new Hashtable() {
            { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
            { "mapName", mapName }
        };
        ro.CustomRoomPropertiesForLobby = new[] { "mapSceneIndex", "mapName" };

        // Nome da sala fixo para todos entrarem na mesma (sem random)
        string roomName = "Room_" + mapName;
        PhotonNetwork.JoinOrCreateRoom(roomName, ro, typedLobby: null);
    }

    // --- SAIR DO JOGO ---
    public void LeaveGameAndGoToMenu(string menuSceneName)
    {
        Time.timeScale = 1f;
        sceneToLoadOnLeave = menuSceneName;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        else { SceneManager.LoadScene(menuSceneName); Destroy(this.gameObject); }
    }

    // --- CALLBACKS PHOTON ---
    public override void OnConnectedToMaster() { JoinRoomLogic(); }
    public override void OnDisconnected(DisconnectCause cause) { if (connectigUI != null) connectigUI.SetActive(false); if (nameUI != null) nameUI.SetActive(true); }

    public override void OnJoinedRoom()
    {
        if (connectigUI != null) connectigUI.SetActive(false);

        // 1. Reset Score e Vidas na Rede
        PhotonNetwork.LocalPlayer.SetScore(0);
        SetInitialRespawnCount(PhotonNetwork.LocalPlayer);

        // 2. Chama o Lobby para mostrar o botão START
        if (LobbyManager.instance != null)
        {
            Debug.Log("LobbyManager encontrado. A mostrar sala de espera...");
            LobbyManager.instance.OnRoomEntered();
        }
        else
        {
            Debug.Log("Sem LobbyManager. A iniciar jogo direto.");
            StartGame();
        }
    }

    public override void OnLeftRoom()
    {
        if (!string.IsNullOrEmpty(sceneToLoadOnLeave))
        {
            SceneManager.LoadScene(sceneToLoadOnLeave);
            if (instance == this) Destroy(this.gameObject);
            sceneToLoadOnLeave = "";
        }
    }

    // --- INÍCIO DE JOGO (PvP) ---
    public void StartGame()
    {
        Debug.Log("O Jogo PvP Começou!");

        // Desliga a câmara de espera
        if (roomCam != null) roomCam.SetActive(false);

        // Faz o spawn do jogador (Guerreiro)
        RespawnPlayer();

        // NOTA: Como é PvP, não há spawn de inimigos AI aqui.
    }

    // --- SISTEMA DE MORTE E VIDAS (LOCAL) ---
    public void HandleMyDeath()
    {
        int currentRespawns = GetRespawnCount(PhotonNetwork.LocalPlayer);

        if (currentRespawns >= 0)
        {
            currentRespawns--;
            Hashtable props = new Hashtable { { RESPAWN_COUNT_KEY, currentRespawns } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        if (currentRespawns >= 0)
        {
            Debug.Log($"A fazer respawn... Vidas restantes: {currentRespawns}");
            RespawnPlayer();
        }
        else
        {
            Debug.Log("Vidas esgotadas! GAME OVER.");
            if (roomCam != null) roomCam.SetActive(true); // Liga câmara de espectador
            if (endScreen != null) endScreen.ShowDefeat(); // Mostra Derrota
        }
    }

    // --- VERIFICAÇÃO DE VITÓRIA (PvP - Last Man Standing) ---

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        CheckWinCondition();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // Se alguém perdeu uma vida, vamos ver se só sobro eu
        if (changedProps.ContainsKey(RESPAWN_COUNT_KEY))
        {
            CheckWinCondition();
        }
    }

    private void CheckWinCondition()
    {
        // 1. Se eu já perdi (vidas < 0), nunca posso ganhar
        if (GetRespawnCount(PhotonNetwork.LocalPlayer) < 0) return;

        // 2. Conta quantos jogadores estão VIVOS na sala (incluindo eu)
        int activePlayers = 0;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (GetRespawnCount(p) >= 0) activePlayers++;
        }

        // 3. Verifica se o jogo já começou oficialmente (para não ganhar no lobby de espera)
        bool gameStarted = false;
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("gs"))
        {
            gameStarted = (bool)PhotonNetwork.CurrentRoom.CustomProperties["gs"];
        }

        // 4. CONDIÇÃO DE VITÓRIA:
        // - O jogo já começou
        // - E só sobra 1 jogador vivo (EU)
        if (gameStarted && activePlayers == 1)
        {
            Debug.Log("VITÓRIA! És o único sobrevivente (Last Man Standing).");
            if (endScreen != null) endScreen.ShowVictory();
        }
    }

    // --- SPAWN DO JOGADOR ---
    public void SetInitialRespawnCount(Player player)
    {
        if (!player.CustomProperties.ContainsKey(RESPAWN_COUNT_KEY))
        {
            Hashtable props = new Hashtable { { RESPAWN_COUNT_KEY, MAX_RESPAWNS } };
            player.SetCustomProperties(props);
        }
    }

    public void RespawnPlayer()
    {
        // 1. Verifica se temos pontos de spawn
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[RoomManager] Erro: Sem pontos de spawn!");
            return;
        }

        // --- AQUI ESTAVA O ERRO ---
        // Antigamente tinhas aqui: if (player == null) return;
        // REMOVEMOS ISSO porque agora carregamos por NOME, não pelo Inspector.

        int playerIndex = GetPlayerIndex(PhotonNetwork.LocalPlayer);
        int spawnIndex = playerIndex % spawnPoints.Length;
        Transform spawnPoint = spawnPoints[spawnIndex];

        // 2. Determina qual boneco criar
        string charName = PlayerPrefs.GetString("SelectedCharacter", "Soldier");

        // Proteção extra: se o nome vier vazio ou errado, usa Soldier
        if (string.IsNullOrEmpty(charName) || charName == "None")
            charName = "Soldier";

        Debug.Log($"[RoomManager] A fazer spawn de: {charName}");

        // 3. Cria o boneco
        GameObject _player = PhotonNetwork.Instantiate(charName, spawnPoint.position, Quaternion.identity);

        // 4. Configurações
        _player.GetComponent<PlayerSetup>()?.IsLocalPlayer();

        Health h = _player.GetComponent<Health>();
        if (h != null) h.isLocalPlayer = true;

        if (_player.GetComponent<PhotonView>() != null)
        {
            _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
            PhotonNetwork.LocalPlayer.NickName = nickName;
        }
    }
    // --- UTILITÁRIOS ---
    private int GetRespawnCount(Player player)
    {
        if (player.CustomProperties.TryGetValue(RESPAWN_COUNT_KEY, out object count)) return (int)count;
        return MAX_RESPAWNS;
    }

    private int GetPlayerIndex(Player player)
    {
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++) { if (players[i] == player) return i; }
        return 0;
    }
}