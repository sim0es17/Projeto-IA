using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using Photon.Realtime;

public class TGRoomManager : MonoBehaviourPunCallbacks
{
    public static TGRoomManager instance;

    [Header("Player")]
    public GameObject player;
    public Transform[] spawnPoints;

    [Header("Enemy Setup")]
    public GameObject enemyPrefab;
    public Transform[] enemySpawnPoints;
    public int enemyCount = 3;
    public float enemyRespawnDelay = 5f;
    public int maxRespawnsPerEnemy = 2;

    [Space]
    public GameObject tgRoomCam;

    private List<GameObject> activeEnemies = new List<GameObject>();
    private int[] enemyRespawnCounts;

    void Awake()
    {
        // Garante que só existe um Manager por cena
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void Start()
    {
        // --- CORREÇÃO DO ERRO "ALREADY CONNECTED" ---
        // Verifica se já estamos ligados ao Photon (porque viemos de outra arena)
        if (PhotonNetwork.IsConnectedAndReady)
        {
            Debug.Log("Já estamos ligados ao Photon! A entrar no Lobby...");

            // Se já estivermos numa sala antiga, saímos primeiro
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
                // O callback OnLeftRoom trataria do resto, mas para simplificar,
                // vamos deixar o Photon gerir a saída e reentrada.
            }
            else
            {
                // Se estamos no Master Server, entramos no Lobby diretamente
                OnConnectedToMaster();
            }
        }
        else
        {
            Debug.Log("Não estamos ligados. A conectar...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // --- FUNÇÕES DE CONEXÃO E SALA ---

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Training Ground Lobby");

        Photon.Realtime.RoomOptions roomOptions = new Photon.Realtime.RoomOptions
        {
            MaxPlayers = 1,
            IsVisible = false,
            IsOpen = true
        };

        // --- CORREÇÃO DE NOME DA SALA ---
        // Usamos o nome da cena atual para criar a sala.
        // Assim, quem está na "Arena 2" não entra na sala da "Arena 3".
        string currentSceneName = SceneManager.GetActiveScene().name;
        string roomName = "Room_" + currentSceneName;

        Debug.Log($"A entrar na sala: {roomName}");
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, null);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Player has joined the Room: " + PhotonNetwork.CurrentRoom.Name);

        // Fecha a sala
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }

        tgRoomCam.SetActive(false);

        // Spawn Player
        RespawnPlayer();

        // Spawn Enemies
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnInitialEnemies();
        }
    }

    // Se sairmos de uma sala (ex: vindo da Arena 2), entramos no Lobby para ir para a Arena 3
    public override void OnLeftRoom()
    {
        PhotonNetwork.JoinLobby();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[TGRoomManager] Desconectado. Causa: {cause}");
    }

    // --- RESTO DO CÓDIGO (Igual ao teu original) ---

    public void LeaveGameAndGoToMenu(string menuSceneName)
    {
        DestroyAllActiveEnemies();
        Time.timeScale = 1f;
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        SceneManager.LoadScene(menuSceneName);
        if (instance == this) Destroy(this.gameObject);
    }

    private void DestroyAllActiveEnemies()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        foreach (GameObject enemy in new List<GameObject>(activeEnemies))
        {
            if (enemy != null) PhotonNetwork.Destroy(enemy);
        }
        activeEnemies.Clear();
    }

    public void RespawnPlayer()
    {
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        string prefabName = "Soldier";

        if (CharacterSelection.Instance != null && !string.IsNullOrEmpty(CharacterSelection.Instance.selectedPrefabName))
        {
            prefabName = CharacterSelection.Instance.selectedPrefabName;
        }

        GameObject _player = PhotonNetwork.Instantiate(prefabName, spawnPoint.position, Quaternion.identity);

        PlayerSetup setup = _player.GetComponent<PlayerSetup>();
        if (setup != null) setup.IsLocalPlayer();

        Health health = _player.GetComponent<Health>();
        if (health != null) health.isLocalPlayer = true;
    }

    private void SpawnInitialEnemies()
    {
        activeEnemies.Clear();
        int enemiesToSpawn = Mathf.Min(enemyCount, enemySpawnPoints.Length);
        enemyRespawnCounts = new int[enemiesToSpawn];

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            enemyRespawnCounts[i] = maxRespawnsPerEnemy;
            SpawnSingleEnemy(enemySpawnPoints[i].position, i);
        }
    }

    private void SpawnSingleEnemy(Vector3 position, int spawnIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (enemyPrefab != null)
        {
            object[] data = new object[] { spawnIndex };
            GameObject newEnemy = PhotonNetwork.Instantiate(enemyPrefab.name, position, Quaternion.identity, 0, data);
            activeEnemies.Add(newEnemy);
        }
        else
        {
            Debug.LogError("Enemy Prefab não está atribuído no Room Manager!");
        }
    }

    public void RequestEnemyRespawn(int spawnIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (spawnIndex < 0 || spawnIndex >= enemyRespawnCounts.Length) return;

        if (enemyRespawnCounts[spawnIndex] > 0)
        {
            enemyRespawnCounts[spawnIndex]--;
            Vector3 respawnPosition = enemySpawnPoints[spawnIndex].position;
            StartCoroutine(EnemyRespawnRoutine(enemyRespawnDelay, respawnPosition, spawnIndex));
        }
    }

    private IEnumerator EnemyRespawnRoutine(float delay, Vector3 position, int spawnIndex)
    {
        yield return new WaitForSeconds(delay);
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnSingleEnemy(position, spawnIndex);
        }
    }
}