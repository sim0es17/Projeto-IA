using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using Photon.Pun.UtilityScripts;
using System.Collections;
using TMPro; 

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
    public GameObject roomCam;
    public GameObject nameUI;
    public GameObject connectigUI;

    // ARRASTA O TEU HUDCANVAS PARA AQUI
    public MultiplayerEndScreen endScreen;

    [Header("Room Info")]
    public string mapName = "Noname";
    private string nickName = "Nameless"; // Variável local que guarda o nome do input da UI

    public bool IsNamePanelActive => nameUI != null && nameUI.activeSelf;

    void Awake()
    {
        // Padrão Singleton com DontDestroyOnLoad
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    // Chamado pelo campo de Input da UI (EndEdit ou ValueChange)
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
        // Se o nome não foi digitado, podemos definir um padrão ou avisar
        if (string.IsNullOrEmpty(nickName) || nickName == "Nameless")
        {
             Debug.LogWarning("Por favor, digite um nome antes de entrar.");
             // Pode adicionar aqui lógica para vibrar ou destacar o campo de input
             return;
        }

        // =========================================================================
        // CÓDIGO CHAVE: DEFINE O NICKNAME ANTES DE QUALQUER TENTATIVA DE CONEXÃO/ENTRADA
        // Garante que o nome digitado na UI está na propriedade de rede.
        PhotonNetwork.NickName = nickName;
        // =========================================================================

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
    public override void OnConnectedToMaster() 
    { 
        // =========================================================================
        // CÓDIGO DE SEGURANÇA: Se o nome não foi definido antes (via JoinRoomButtonPressed),
        // garante que ele seja definido aqui antes de entrar no lobby.
        if (!string.IsNullOrEmpty(nickName) && PhotonNetwork.NickName != nickName) 
        {
            PhotonNetwork.NickName = nickName;
        }
        // =========================================================================
        
        JoinRoomLogic(); 
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (connectigUI != null) connectigUI.SetActive(false);
        if (nameUI != null) nameUI.SetActive(true);
    }

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

        // Garante que a câmara de sala está ativa antes de spawnar
        if (roomCam != null) roomCam.SetActive(true);

        // Faz o spawn do jogador (Guerreiro)
        RespawnPlayer();

        // Desliga a câmara de espera APÓS spawnar
        if (roomCam != null) roomCam.SetActive(false);
    }

    // --- SISTEMA DE MORTE E VIDAS (LOCAL) ---
    public void HandleMyDeath()
    {
        Debug.Log("[RoomManager] HandleMyDeath chamado.");

        // 1. TENTA ATIVAR A CÂMARA DE ESPECTADOR
        if (roomCam != null)
        {
            roomCam.SetActive(true);
        }

        // Garante que o menu de login e conexão desaparecem enquanto estás morto
        if (nameUI != null) nameUI.SetActive(false);
        if (connectigUI != null) connectigUI.SetActive(false);

        int currentRespawns = GetRespawnCount(PhotonNetwork.LocalPlayer);

        // Retira uma vida
        if (currentRespawns >= 0)
        {
            currentRespawns--;
            Hashtable props = new Hashtable { { RESPAWN_COUNT_KEY, currentRespawns } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        // Verifica se ainda pode fazer respawn
        if (currentRespawns >= 0)
        {
            Debug.Log($"A preparar respawn... Vidas restantes: {currentRespawns}");
            // Inicia o temporizador de 3 segundos
            StartCoroutine(RespawnCoroutine(3.0f));
        }
        else
        {
            Debug.Log("Vidas esgotadas! GAME OVER.");
            if (endScreen != null) endScreen.ShowDefeat();
        }
    }

    // --- NOVA CORROTINA DE RESPAWN ---
    private IEnumerator RespawnCoroutine(float delay)
    {
        // Espera X segundos
        yield return new WaitForSeconds(delay);

        // Cria o novo boneco
        RespawnPlayer();

        // FIX: Espera 1 frame para garantir que o Start do Player correu e ativou a câmara dele
        yield return null;

        // Desliga a câmara de espectador
        if (roomCam != null) roomCam.SetActive(false);
    }

    // --- VERIFICAÇÃO DE VITÓRIA (PvP - Last Man Standing) ---

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        CheckWinCondition();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(RESPAWN_COUNT_KEY))
        {
            CheckWinCondition();
        }
    }

    private void CheckWinCondition()
    {
        if (GetRespawnCount(PhotonNetwork.LocalPlayer) < 0) return;

        int activePlayers = 0;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (GetRespawnCount(p) >= 0) activePlayers++;
        }

        bool gameStarted = false;
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("gs"))
        {
            gameStarted = (bool)PhotonNetwork.CurrentRoom.CustomProperties["gs"];
        }

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
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[RoomManager] Erro: Array 'spawnPoints' está vazio!");
            return;
        }

        int playerIndex = GetPlayerIndex(PhotonNetwork.LocalPlayer);
        int spawnIndex = playerIndex % spawnPoints.Length;

        // Verificação extra para evitar IndexOutOfRange
        if (spawnIndex >= spawnPoints.Length) spawnIndex = 0;

        Transform spawnPoint = spawnPoints[spawnIndex];

        // Vai buscar o nome do boneco guardado
        string charName = PlayerPrefs.GetString("SelectedCharacter", "Soldier");
        if (string.IsNullOrEmpty(charName) || charName == "None") charName = "Soldier";

        Debug.Log($"[RoomManager] A fazer spawn de: {charName}");

        try
        {
            // O RPC para SetNickname será chamado no Start() do PlayerSetup, 
            // usando o PhotonNetwork.LocalPlayer.NickName (que definimos no OnConnectedToMaster ou JoinRoomButtonPressed).
            GameObject _player = PhotonNetwork.Instantiate(charName, spawnPoint.position, Quaternion.identity);

            if (_player != null)
            {
                _player.GetComponent<PlayerSetup>()?.IsLocalPlayer();

                Health h = _player.GetComponent<Health>();
                if (h != null) h.isLocalPlayer = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RoomManager] ERRO AO INSTANCIAR JOGADOR: {ex.Message}");
            Debug.LogError("Dica: Verifica se o Prefab está dentro da pasta 'Resources' e se o nome está correto.");
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
