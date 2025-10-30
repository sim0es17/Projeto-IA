using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// TGRoomManager should inherit from MonoBehaviourPunCallbacks to use Photon callback methods
public class TGRoomManager : MonoBehaviourPunCallbacks
{
    public static TGRoomManager instance;

    public GameObject player;

    [Space]
    public Transform[] spawnPoints;

    [Space]
    public GameObject tgRoomCam;

    void Awake()
    {
        instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Connecting...");

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();

        Debug.Log("Connected to Master");

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();

        Debug.Log("Joined Training Ground");

        PhotonNetwork.JoinOrCreateRoom("TrainingGroundRoom", new Photon.Realtime.RoomOptions { MaxPlayers = 1 }, null);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        Debug.Log("Player has joined the Training Ground");

        tgRoomCam.SetActive(false);

        RespawnPlayer();
    }

    public void RespawnPlayer()
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
        _player.GetComponent<PlayerSetup>().IsLocalPlayer();
        _player.GetComponent<Health>().isLocalPlayer = true;
    }
}
