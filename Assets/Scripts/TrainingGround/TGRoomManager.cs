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

    [Space]
    public GameObject tgRoomCam;

    private List<GameObject> activeEnemies = new List<GameObject>();

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

        // 1. Cria as opções da sala
        Photon.Realtime.RoomOptions roomOptions = new Photon.Realtime.RoomOptions
        {
            MaxPlayers = 1,              // Limita a 1 jogador (tu)
            IsVisible = false,           // Torna a sala INVISÍVEL no Lobby e exclui de JoinRandomRoom
            IsOpen = true                // Mantém a sala aberta para que possas entrar pelo nome
        };

        // 2. Tenta entrar ou criar a sala com as novas opções
        PhotonNetwork.JoinOrCreateRoom("TrainingGroundRoom", roomOptions, null);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        // NOVO: Fecha a sala para impedir que mais alguém entre
        // (Isto só funciona se IsMasterClient for true, o que és, pois criaste/entraste)
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

    // -------------------------------------------------------------
    // --- Lógica de Spawn e Respawn de Inimigos (Modificada) ---
    // -------------------------------------------------------------

    private void SpawnInitialEnemies()
    {
        activeEnemies.Clear();

        // Limita o número de inimigos ao número de pontos de spawn disponíveis
        int enemiesToSpawn = Mathf.Min(enemyCount, enemySpawnPoints.Length);

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            // Usa o índice 'i' para garantir que cada inimigo usa o seu próprio ponto de spawn (0, 1, 2...)
            Transform spawnPoint = enemySpawnPoints[i];

            SpawnSingleEnemy(spawnPoint.position);
        }
        Debug.Log($"Master Client spawnou {enemiesToSpawn} inimigos nos seus pontos designados.");
    }

    private void SpawnSingleEnemy(Vector3 position)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (enemyPrefab != null)
        {
            GameObject newEnemy = PhotonNetwork.Instantiate(enemyPrefab.name, position, Quaternion.identity);

            activeEnemies.Add(newEnemy);
        }
        else
        {
            Debug.LogError("Enemy Prefab não está atribuído no Room Manager!");
        }
    }

    public void RequestEnemyRespawn(Vector3 deathPosition)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"Inimigo foi destruído. Respawn agendado em {enemyRespawnDelay} segundos.");

        // Encontra o ponto de spawn original mais próximo da posição de morte
        Vector3 respawnPosition = FindClosestSpawnPoint(deathPosition);

        StartCoroutine(EnemyRespawnRoutine(enemyRespawnDelay, respawnPosition));
    }

    private IEnumerator EnemyRespawnRoutine(float delay, Vector3 position)
    {
        yield return new WaitForSeconds(delay);

        if (PhotonNetwork.IsMasterClient)
        {
            SpawnSingleEnemy(position);
            Debug.Log("Inimigo respawnado.");
        }
    }

    /// <summary>
    /// Encontra o ponto de spawn original que está mais próximo da posição de morte do inimigo.
    /// </summary>
    private Vector3 FindClosestSpawnPoint(Vector3 deathPosition)
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            Debug.LogError("Nenhum ponto de spawn de inimigo atribuído!");
            return deathPosition;
        }

        Transform closestPoint = enemySpawnPoints[0];
        float minDistance = Vector3.Distance(deathPosition, closestPoint.position);

        for (int i = 1; i < enemySpawnPoints.Length; i++)
        {
            float distance = Vector3.Distance(deathPosition, enemySpawnPoints[i].position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = enemySpawnPoints[i];
            }
        }
        return closestPoint.position;
    }
}
