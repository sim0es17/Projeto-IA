using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.SceneManagement;

public class RoomList : MonoBehaviourPunCallbacks
{
    public static RoomList Instance;

    [Header("UI (Listagem)")]
    public Transform roomListParent;
    public GameObject roomListItemPrefab;

    [Header("UI (Painéis)")]
    public GameObject lobbyPanel; // O painel "Choose a game" (Visão das salas)
    public GameObject createRoomPanel; // O painel "Pick a room name" (Criação de sala)

    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();
    private string cachedRoomNameToCreate;

    private void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        // Garante que o painel de lobby está visível e o de criar sala está escondido
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (createRoomPanel != null) createRoomPanel.SetActive(false);

        // Precautions: Se for persistente e já estiver numa sala, sai e desconecta
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            // NUNCA chamar disconnect aqui se o RoomManager for carregar a cena após a desconexão.
            // Apenas o RoomManager deve controlar a desconexão/conexão global do jogo.
        }

        // Se você precisa do RoomList para gerenciar a conexão ao Photon Lobby:
        if (!PhotonNetwork.IsConnected)
        {
            yield return new WaitUntil(() => !PhotonNetwork.IsConnected);
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log("Connected to Master Server. Joining Lobby...");
        PhotonNetwork.JoinLobby();
    }

    // --- Funções de Criação e Entrada em Sala ---

    public void ChangeRoomToCreateName(string _roomName)
    {
        cachedRoomNameToCreate = _roomName;
    }

    public void CreateRoomByIndex(int sceneIndex)
    {
        JoinRoomByName(cachedRoomNameToCreate, sceneIndex);
    }

    public void JoinRoomByName(string _name, int _sceneIndex)
    {
        PlayerPrefs.SetString("RoomNameToJoin", _name);

        // Carrega a cena onde o RoomManager existe para que ele possa entrar na sala.
        SceneManager.LoadScene(_sceneIndex);
    }

    // --- Controlo de Painéis ---

    public void ShowCreateRoomPanel()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (createRoomPanel != null) createRoomPanel.SetActive(true);
    }

    public void GoBackToLobbyPanel()
    {
        if (createRoomPanel != null) createRoomPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
    }

    // --- FUNÇÃO CORRIGIDA PARA REGRESSAR AO MENU PRINCIPAL ---

    /**
     * Esta função é chamada quando o utilizador quer sair do lobby/lista de salas e voltar para a Cena 1.
     * Ela delega a limpeza e o carregamento da cena ao RoomManager (se ele estiver persistente)
     * para garantir que não há UI sobreposta.
     */
    public void GoBackToMainMenu()
    {
        // 1. Tenta usar o RoomManager (se ele for persistente da Cena 3 e ainda estiver ativo)
        if (RoomManager.instance != null)
        {
            Debug.Log("RoomList chamando RoomManager para gerir o retorno e limpeza.");

            // O RoomManager irá desconectar o Photon, destruir a si próprio e o LobbyManager, 
            // e carregar o MainMenu.
            RoomManager.instance.GoToMainMenu();
        }
        else
        {
            // 2. Se o RoomManager não estiver ativo (p. ex., se esta cena foi chamada diretamente
            //    do Main Menu e estamos apenas conectados ao Photon Lobby),
            //    desconectamos e carregamos a cena MainMenu diretamente.
            if (PhotonNetwork.IsConnected)
            {
                Debug.Log("RoomList desconectando do Photon e carregando MainMenu.");
                PhotonNetwork.Disconnect();
            }
            SceneManager.LoadScene("MainMenu");
        }
    }

    // --- Callbacks do Photon (Listagem de Salas) ---

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // Limpa a lista cacheada e a UI
        foreach (Transform roomItem in roomListParent)
        {
            Destroy(roomItem.gameObject);
        }
        cachedRoomList.Clear();

        // Atualiza a lista cacheada
        foreach (var room in roomList)
        {
            if (room.RemovedFromList)
            {
                // Sala removida
                continue;
            }
            cachedRoomList.Add(room);
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        if (roomListParent == null || roomListItemPrefab == null) return;

        // Limpa a UI
        foreach (Transform roomItem in roomListParent)
        {
            Destroy(roomItem.gameObject);
        }

        // Recria os itens da UI
        foreach (var room in cachedRoomList)
        {
            // Assumimos que o código de criação de RoomItemButton está correto...
            GameObject roomItem = Instantiate(roomListItemPrefab, roomListParent);

            string roomMapName = "Unknown";
            object sceneIndexObject;
            int roomSceneIndex = 1;

            // Tenta obter o nome do mapa
            if (room.CustomProperties.TryGetValue("mapName", out object mapNameObject))
            {
                roomMapName = (string)mapNameObject;
            }

            // Tenta obter o índice da cena (para o JoinRoomByName)
            if (room.CustomProperties.TryGetValue("mapSceneIndex", out sceneIndexObject))
            {
                roomSceneIndex = (int)sceneIndexObject;
            }

            // Atualiza os TextMeshPro
            roomItem.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = $"{room.Name} ({roomMapName})";
            roomItem.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = $"{room.PlayerCount} /4";

            // Configura os dados no componente RoomItemButton (assumindo que existe)
            roomItem.GetComponent<RoomItemButton>().RoomName = room.Name;
            roomItem.GetComponent<RoomItemButton>().SceneIndex = roomSceneIndex;
        }
    }
}