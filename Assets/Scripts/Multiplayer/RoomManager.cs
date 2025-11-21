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

    // Variável para guardar o nome da cena a carregar após sair da sala
    private string sceneToLoadOnLeave = "";

    [Header("Player and Spawn")]
    public GameObject player;
    // O array de pontos de spawn: cada índice corresponde a um jogador (PlayerList[i])
    public Transform[] spawnPoints; 

    [Header("UI References")]
    public GameObject roomCam; // A câmara usada no lobby/espera
    public GameObject nameUI; // UI para inserir o nome/menu principal
    public GameObject connectigUI; // UI de 'A Conectar...'

    // Propriedade para verificar se o painel de nome está ativo (usado pelo PauseMenuController)
    public bool IsNamePanelActive => nameUI != null && nameUI.activeSelf;

    private string nickName = "Nameless";
    public string mapName = "Noname"; // Nome do mapa para exibir no lobby/propriedades da sala

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

    // --- FUNÇÕES DE CONEXÃO E SALA ---

    public void ConnectToMaster()
    {
        // 1. Inicia a conexão com o Photon Master Server
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("Tentando conectar ao Photon Master Server...");

            // Certifique-se que as UIs existem antes de manipulá-las
            if (nameUI != null) nameUI.SetActive(false);
            if (connectigUI != null) connectigUI.SetActive(true);
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

        // Propriedades da sala para sincronização
        ro.CustomRoomProperties = new Hashtable()
        {
            { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
            { "mapName", mapName }
        };

        // Propriedades visíveis no Lobby
        ro.CustomRoomPropertiesForLobby = new[]
        {
            "mapSceneIndex",
            "mapName"
        };

        // Entra ou cria a sala com as opções definidas
        PhotonNetwork.JoinOrCreateRoom(roomName: PlayerPrefs.GetString(key: "RoomNameToJoin"), ro, typedLobby: null);
    }

    // --- FUNÇÃO PARA SAIR E IR PARA O MENU (LIGADA AO TEU BOTÃO) ---

    /// <summary>
    /// Função pública chamada pelo botão de pausa para sair da sala e ir para o menu.
    /// </summary>
    public void LeaveGameAndGoToMenu(string menuSceneName)
    {
        Debug.Log("A sair do jogo e a voltar ao menu...");

        // 1. Volta a pôr o tempo a 1, caso estivesse em pausa
        Time.timeScale = 1f;

        // 2. Guarda o nome da cena do menu para carregar mais tarde
        sceneToLoadOnLeave = menuSceneName;

        // 3. Manda o Photon sair da sala (isto é assíncrono)
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            // Fallback para caso não esteja numa sala, mas o jogo tenha começado
            SceneManager.LoadScene(menuSceneName);
            Destroy(this.gameObject);
        }
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
        if (connectigUI != null) connectigUI.SetActive(false);
        if (nameUI != null) nameUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log("Joined room!");

        if (connectigUI != null) connectigUI.SetActive(false); // Esconde a tela de connecting

        // **** CORREÇÃO CRUCIAL PARA INICIALIZAÇÃO DE RESPAWN ****
        // Define a contagem inicial de respawns assim que o jogador entra na sala.
        SetInitialRespawnCount(PhotonNetwork.LocalPlayer);
        // ******************************************************
        
        // 1. Chama o LobbyManager para iniciar a lógica de espera (UI do Lobby)
        if (LobbyManager.instance != null)
        {
            // Assume que 'LobbyManager' existe e tem uma instância e o método 'OnRoomEntered'
            LobbyManager.instance.OnRoomEntered();
        }
    }

    // Chamado após o jogador sair da sala (LeaveRoom())
    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        Debug.Log("Saída da sala com sucesso (OnLeftRoom).");

        // Agora que saímos da sala, verifica se tínhamos uma cena para carregar
        if (!string.IsNullOrEmpty(sceneToLoadOnLeave))
        {
            // Carrega a cena do menu
            SceneManager.LoadScene(sceneToLoadOnLeave);

            // Destrói o RoomManager para que não vá para a cena do menu
            if (instance == this)
            {
                Destroy(this.gameObject);
            }
            // Limpa a variável
            sceneToLoadOnLeave = "";
        }
    }


    // --- LÓGICA DE RESPAWN E MORTE ---

    public void SetInitialRespawnCount(Player player)
    {
        // Define o número inicial de respawns apenas se a propriedade não existir
        if (!player.CustomProperties.ContainsKey(RESPAWN_COUNT_KEY))
        {
            Hashtable props = new Hashtable();
            // Define 2 respawns restantes (além do spawn inicial)
            props.Add(RESPAWN_COUNT_KEY, MAX_RESPAWNS);
            player.SetCustomProperties(props);
            Debug.Log($"Jogador {player.NickName} inicializado com {MAX_RESPAWNS} respawns restantes.");
        }
    }

    public void RespawnPlayer()
    {
        // Obtém a contagem de respawns restantes.
        int respawnsLeft = GetRespawnCount(PhotonNetwork.LocalPlayer);

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("O Array 'spawnPoints' está vazio ou nulo! Não é possível dar Respawn.");
            return;
        }

        // O jogador pode ter respawn *se* tiver chances restantes (respawnsLeft >= 0)
        // NOTA: Se o jogador tiver 0 respawns restantes, esta função NÃO deve ser chamada 
        // ou a sua lógica deve ser revista. Assumindo que 0 respawns = 1 vida restante.
        // O ponto de falha agora é o limite < 0.
        if (respawnsLeft >= 0 && player != null)
        {
            // 1. Obtém a lista de jogadores na sala
            Player[] players = PhotonNetwork.PlayerList;
            
            // 2. Encontra o índice do jogador local na lista
            int playerIndex = -1;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == PhotonNetwork.LocalPlayer)
                {
                    playerIndex = i;
                    break;
                }
            }

            // 3. Seleção do Ponto de Spawn
            Transform spawnPoint;
            int selectedSpawnIndex = playerIndex; // Começa pelo índice do jogador

            if (selectedSpawnIndex >= 0 && selectedSpawnIndex < spawnPoints.Length)
            {
                // Usa o ponto de spawn baseado no índice do jogador
                spawnPoint = spawnPoints[selectedSpawnIndex];
            }
            else
            {
                // Fallback: Se o índice for inválido ou exceder o número de spawns, usa o primeiro (0)
                selectedSpawnIndex = 0;
                // Previne IndexOutOfRangeException caso spawnPoints esteja vazio, 
                // embora o check inicial já o faça.
                if (spawnPoints.Length > 0)
                {
                    spawnPoint = spawnPoints[0];
                }
                else
                {
                    // Se estiver vazio (o que o check inicial deveria ter apanhado)
                    Debug.LogError("SpawnPoints está vazio no fallback. Abortando Respawn.");
                    return; 
                }
                Debug.LogWarning($"Índice de jogador ({playerIndex}) inválido para Spawn. Usando SpawnPoint #0 como fallback.");
            }

            // 4. Instancia o player em rede
            GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);

            // Configurações do jogador local
            _player.GetComponent<PlayerSetup>()?.IsLocalPlayer();

            // Define e sincroniza o Nickname (necessário ter um RPC chamado "SetNickname" no PlayerSetup)
            if (_player.GetComponent<PhotonView>() != null)
            {
                _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
                PhotonNetwork.LocalPlayer.NickName = nickName;
            }

            Debug.Log($"Respawn realizado para {PhotonNetwork.LocalPlayer.NickName} no SpawnPoint #{selectedSpawnIndex}. Respawn(s) restante(s) antes da próxima morte: {respawnsLeft}");
        }
        else
        {
            // O jogador atingiu o limite de respawns ou faltam referências
            Debug.Log($"LIMITE DE RESPAWN ATINGIDO! Jogador {PhotonNetwork.LocalPlayer.NickName} não pode mais dar respawn.");
            
            // Lógica de Fim de Jogo (opcional): 
            // Se quiser forçar o jogador a sair ou mostrar uma tela de game over.
        }
    }

    // Chamado pelo script Health.cs (no MasterClient, para atualizar a contagem de respawn na rede)
    public void OnPlayerDied(Player playerWhoDied)
    {
        // APENAS o MasterClient deve manipular e sincronizar a contagem de respawns
        if (!PhotonNetwork.IsMasterClient) return;

        int currentRespawnCount = GetRespawnCount(playerWhoDied);

        // Só decrementamos se o jogador tiver respawns restantes (currentRespawnCount > 0)
        // NOTA: Se respawnCount for 0, o respawn será permitido, mas a contagem passará a -1, 
        // bloqueando o próximo respawn.
        if (currentRespawnCount > 0)
        {
            // Decrementa a contagem de respawns
            currentRespawnCount--;

            Hashtable props = new Hashtable();
            props.Add(RESPAWN_COUNT_KEY, currentRespawnCount);
            playerWhoDied.SetCustomProperties(props);

            Debug.Log($"[MasterClient] Jogador {playerWhoDied.NickName} morreu. Restam {currentRespawnCount} respawn(s).");
        }
        // Se currentRespawnCount for 0 ou menor, a contagem não é alterada (e o respawn falhará na próxima vez).
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
        // Nota: A lista PhotonNetwork.PlayerList é reordenada automaticamente após um jogador sair.
    }
}
