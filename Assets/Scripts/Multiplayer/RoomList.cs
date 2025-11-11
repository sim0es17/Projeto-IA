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

    // !!! NOVAS VARIÁVEIS ADICIONADAS !!!
    [Header("UI (Painéis)")]
    public GameObject lobbyPanel; // O painel "Choose a game"
    public GameObject createRoomPanel; // O painel "Pick a room name"
    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();
    private string cachedRoomNameToCreate;

    public void ChangeRoomToCreateName(string _roomName)
    {
        cachedRoomNameToCreate = _roomName;
    }

    public void CreateRoomByIndex(int sceneIndex)
    {
        // Esta função está perfeita para os teus botões "Create Room in Arena 1/2"
        JoinRoomByName(cachedRoomNameToCreate, sceneIndex);
    }

    private void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        // Boa prática: garantir que o painel de lobby está visível
        // e o de criar sala está escondido ao iniciar.
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (createRoomPanel != null) createRoomPanel.SetActive(false);

        // Precautions
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.Disconnect();
        }

        yield return new WaitUntil(() => !PhotonNetwork.IsConnected);

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log("Connected to Master Server");
        PhotonNetwork.JoinLobby();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // ... (o resto desta função está igual e correto) ...
        if (cachedRoomList.Count <= 0)
        {
            cachedRoomList = roomList;
        }
        else
        {
            foreach (var room in roomList)
            {
                for (int i = 0; i < cachedRoomList.Count; i++)
                {
                    if (cachedRoomList[i].Name == room.Name)
                    {
                        List<RoomInfo> newList = cachedRoomList;

                        if (room.RemovedFromList)
                        {
                            newList.Remove(newList[i]);
                        }
                        else
                        {
                            newList[i] = room;
                        }

                        cachedRoomList = newList;
                    }
                }
            }
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        // ... (o resto desta função está igual e correto) ...
        foreach (Transform roomItem in roomListParent)
        {
            Destroy(roomItem.gameObject);
        }

        foreach (var room in cachedRoomList)
        {
            GameObject roomItem = Instantiate(roomListItemPrefab, roomListParent);

            string roomMapName = "Unknown";

            object mapNameObject;
            if (room.CustomProperties.TryGetValue("mapName", out mapNameObject))
            {
                roomMapName = (string)mapNameObject;
            }

            roomItem.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = room.Name + "(" + roomMapName + ")";
            roomItem.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = room.PlayerCount + " /4";

            roomItem.GetComponent<RoomItemButton>().RoomName = room.Name;

            int roomSceneIndex = 1;

            object sceneIndexObject;
            if (room.CustomProperties.TryGetValue("mapSceneIndex", out sceneIndexObject))
            {
                roomSceneIndex = (int)sceneIndexObject;
            }

            roomItem.GetComponent<RoomItemButton>().SceneIndex = roomSceneIndex;
        }
    }

    public void JoinRoomByName(string _name, int _sceneIndex)
    {
        PlayerPrefs.SetString("RoomNameToJoin", _name);

        // Esta linha pode dar problemas se o script estiver no mesmo objeto
        // que os painéis. Se o menu desaparecer, remove a linha abaixo.
        // gameObject.SetActive(false); 

        SceneManager.LoadScene(_sceneIndex);
        // Load the relavant room 
    }


    // Esta função é para o "Back" do Lobby -> Menu Principal
    public void GoBackToMainMenu()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        SceneManager.LoadScene("MainMenu"); // Continua correta
    }

    //
    // !!! NOVAS FUNÇÕES ADICIONADAS !!!
    //

    /**
     * Esta função é para o botão "Create a room" (no LobbyPanel).
     * Esconde o Lobby e mostra o painel de criação de sala.
     */
    public void ShowCreateRoomPanel()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (createRoomPanel != null) createRoomPanel.SetActive(true);
    }

    /**
     * Esta função é para o botão "Back" (no CreateRoomPanel).
     * Esconde o painel de criação e volta a mostrar o Lobby.
     */
    public void GoBackToLobbyPanel()
    {
        if (createRoomPanel != null) createRoomPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
    }
}
