using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.SceneManagement; // Verifique se isto está incluído!

public class RoomList : MonoBehaviourPunCallbacks
{
    public static RoomList Instance;

    [Header("UI")]
    public Transform roomListParent;
    public GameObject roomListItemPrefab;

    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

    private string cachedRoomNameToCreate;

    public void ChangeRoomToCreateName(string _roomName)
    {
        cachedRoomNameToCreate = _roomName;
    }

    public void CreateRoomByIndex(int sceneIndex)
    {
        // Esta função já existia e está correta.
        // Ela vai chamar a JoinRoomByName, que guarda o nome
        // e carrega a cena do jogo (definida pelo sceneIndex).
        JoinRoomByName(cachedRoomNameToCreate, sceneIndex);
    }

    private void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
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

        gameObject.SetActive(false);

        SceneManager.LoadScene(_sceneIndex);
        // Load the relavant room 
    }

    //
    // !!! NOVA FUNÇÃO ADICIONADA !!!
    //
    public void GoBackToMainMenu()
    {
        // É boa prática desconectar ao voltar para o menu principal
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        // Carrega a cena do Menu Principal
        // !!! MUDA "MenuPrincipal" PARA O NOME EXATO DA TUA CENA !!!
        SceneManager.LoadScene("MainMenu");
    }
}