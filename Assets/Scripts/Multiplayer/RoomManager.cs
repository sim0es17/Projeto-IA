using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;

using UnityEngine.SceneManagement;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;

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
        instance = this;
    }

    public void ChangeNickName(string _name)
    {
        nickName = _name;
    }

    public void JoinRoomButtonPressed()
    {
        Debug.Log("Connecting...");

        RoomOptions ro = new RoomOptions();

        ro.CustomRoomProperties = new Hashtable() 
        {
            { "mapSceneIndex", SceneManager.GetActiveScene().buildIndex },
            { "mapName", mapName }

        };

        ro.CustomRoomPropertiesForLobby = new [] 
        { 
            "mapSceneIndex",
            "mapName"
        };

        PhotonNetwork.JoinOrCreateRoom(roomName:PlayerPrefs.GetString(key:"RoomNameToJoin"), ro, typedLobby:null);

        nameUI.SetActive(false);
        connectigUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log("Joined room!");

        roomCam.SetActive(false);

        RespawnPlayer();
    }

    public void RespawnPlayer()
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
        _player.GetComponent<PlayerSetup>().IsLocalPlayer();
        _player.GetComponent<Health>().isLocalPlayer = true;

        _player.GetComponent<PhotonView>().RPC("SetNickname", RpcTarget.AllBuffered, nickName);
        PhotonNetwork.LocalPlayer.NickName = nickName;
    }
}
