using UnityEngine;
using Photon.Pun;

public class RoomManagerS : MonoBehaviourPunCallbacks
{
    public static RoomManagerS instance;

    public GameObject player;
    public GameObject enemy;

    [Space]
    public Transform[] spawnPoints;
    public Transform[] EspawnPoints;


    [Space]
    public GameObject roomCam;

    private string nickName = "Nameless";

    [Space]
    public GameObject nameUI;
    public GameObject connectigUI;

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

        PhotonNetwork.ConnectUsingSettings();

        nameUI.SetActive(false);
        connectigUI.SetActive(true);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();

        Debug.Log("Connected to Server");

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();

        PhotonNetwork.JoinOrCreateRoom("test", null, null);

        Debug.Log("We're connected and in a room!");

        //GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPoint.position, Quaternion.identity);
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

    public void RespawnEnemy()
    {
        Transform EspawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        GameObject _enemy = PhotonNetwork.Instantiate(enemy.name, EspawnPoint.position, Quaternion.identity);
        _enemy.GetComponent<EnemySetup>().IsLocalEnemy();
        _enemy.GetComponent<EnemyHealth>().IsLocalEnemy = true;
    }
}
