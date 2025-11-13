using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

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

    // Define o limite de respawns
    public int maxRespawnsPerEnemy = 2;

    [Space]
    public GameObject tgRoomCam;

    private List<GameObject> activeEnemies = new List<GameObject>();

    // Array para guardar a contagem de cada spawn point
    private int[] enemyRespawnCounts;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
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

        // Opções para sala privada, invisível e com 1 jogador
        Photon.Realtime.RoomOptions roomOptions = new Photon.Realtime.RoomOptions
        {
            MaxPlayers = 1,
            IsVisible = false, // Invisível no Lobby
            IsOpen = true      // Aberta para tu entrares
        };

        PhotonNetwork.JoinOrCreateRoom("TrainingGroundRoom", roomOptions, null);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        // Fecha a sala imediatamente após entrar
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            Debug.Log("Sala fechada. Ninguém mais pode entrar (IsOpen = false).");
        }

        Debug.Log("Player has joined the Training Ground");

        tgRoomCam.SetActive(false);

        // Player
        RespawnPlayer();

        // Enemy
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnInitialEnemies();
        }
    }

    // --- Player Spawn ---

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

    // -----------------------------------------------------------------
    // --- Lógica de Spawn e Respawn de Inimigos (com Limite) ---
    // -----------------------------------------------------------------

    private void SpawnInitialEnemies()
    {
        activeEnemies.Clear();

        int enemiesToSpawn = Mathf.Min(enemyCount, enemySpawnPoints.Length);

        // Inicializa o array de contagens
        enemyRespawnCounts = new int[enemiesToSpawn];

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            // Define a contagem inicial para este spawn point
            enemyRespawnCounts[i] = maxRespawnsPerEnemy;

            Transform spawnPoint = enemySpawnPoints[i];

            // Passa o índice 'i' para o método de spawn
            SpawnSingleEnemy(spawnPoint.position, i);
        }
        Debug.Log($"Master Client spawnou {enemiesToSpawn} inimigos. Cada um tem {maxRespawnsPerEnemy} respawns.");
    }

    /// <summary>
    /// Instancia um inimigo e passa o seu spawnIndex (índice) através do InstantiationData.
    /// </summary>
    private void SpawnSingleEnemy(Vector3 position, int spawnIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (enemyPrefab != null)
        {
            // Prepara os dados de instanciação (o índice do spawn point)
            object[] data = new object[] { spawnIndex };

            // Instancia o inimigo e passa os 'data'
            GameObject newEnemy = PhotonNetwork.Instantiate(enemyPrefab.name, position, Quaternion.identity, 0, data);

            activeEnemies.Add(newEnemy);
        }
        else
        {
            Debug.LogError("Enemy Prefab não está atribuído no Room Manager!");
        }
    }

    /// <summary>
    /// Chamado pelo EnemyHealth quando morre, passando o seu spawnIndex.
    /// </summary>
    public void RequestEnemyRespawn(int spawnIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Verifica se o índice é válido
        if (spawnIndex < 0 || spawnIndex >= enemyRespawnCounts.Length)
        {
            Debug.LogError($"[TGRoomManager] Recebido pedido de respawn para índice inválido: {spawnIndex}");
            return;
        }

        // Verifica se este spawn point ainda tem respawns
        if (enemyRespawnCounts[spawnIndex] > 0)
        {
            // Se sim, decrementa a contagem
            enemyRespawnCounts[spawnIndex]--;

            Debug.Log($"[TGRoomManager] Inimigo do Spawn Point {spawnIndex} morreu. {enemyRespawnCounts[spawnIndex]} respawns restantes. Agendando respawn.");

            // Pega na posição original do spawn point usando o índice
            Vector3 respawnPosition = enemySpawnPoints[spawnIndex].position;

            // Passa o índice para a rotina
            StartCoroutine(EnemyRespawnRoutine(enemyRespawnDelay, respawnPosition, spawnIndex));
        }
        else
        {
            // Se for 0, não faz respawn
            Debug.Log($"[TGRoomManager] Inimigo do Spawn Point {spawnIndex} morreu. Não há mais respawns.");
        }
    }

    /// <summary>
    /// Aguarda o delay e chama o SpawnSingleEnemy com o índice original.
    /// </summary>
    private IEnumerator EnemyRespawnRoutine(float delay, Vector3 position, int spawnIndex)
    {
        yield return new WaitForSeconds(delay);

        if (PhotonNetwork.IsMasterClient)
        {
            // Faz o respawn do novo inimigo, passando o mesmo índice do spawn point
            SpawnSingleEnemy(position, spawnIndex);
            Debug.Log($"[TGRoomManager] Inimigo respawnado no Spawn Point {spawnIndex}.");
        }
    }
}
