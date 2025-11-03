using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// TGRoomManager should inherit from MonoBehaviourPunCallbacks to use Photon callback methods
public class TGRoomManager : MonoBehaviourPunCallbacks
{
    public static TGRoomManager instance;

    [Header("Spawn")]
    public Transform[] spawnPoints;

    [Space]
    public GameObject tgRoomCam;

    void Awake()
    {
        instance = this;
    }

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

        Debug.Log("Joined Training Ground Lobby");

        PhotonNetwork.JoinOrCreateRoom("TrainingGroundRoom",
            new Photon.Realtime.RoomOptions { MaxPlayers = 1 }, null);
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
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        string prefabName = "Soldier"; // default

        if (CharacterSelection.Instance != null &&
            !string.IsNullOrEmpty(CharacterSelection.Instance.selectedPrefabName))
        {
            prefabName = CharacterSelection.Instance.selectedPrefabName;
        }

        Debug.Log($"[TGRoomManager] Spawning prefab: {prefabName}");

        GameObject _player = PhotonNetwork.Instantiate(
            prefabName,
            spawnPoint.position,
            Quaternion.identity
        );

        PlayerSetup setup = _player.GetComponent<PlayerSetup>();
        if (setup != null)
            setup.IsLocalPlayer();

        Health health = _player.GetComponent<Health>();
        if (health != null)
            health.isLocalPlayer = true;
    }
}
