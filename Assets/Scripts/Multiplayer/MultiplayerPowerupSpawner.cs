using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class MultiplayerPowerupSpawner : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Nomes EXATOS dos prefabs na pasta Resources")]
    public string[] powerupPrefabNames;

    [Header("Área de Spawn")]
    public BoxCollider2D arenaBounds; // A área total onde vamos tentar spawnar
    public LayerMask groundMask;      // O que é considerado "Chão"? (Importante!)

    [Header("Tempo e Ajustes")]
    public float spawnInterval = 15f;
    public float verticalOffset = 0.5f; // Altura extra para não nascer enterrado
    public int maxAttempts = 10;        // Quantas vezes tenta encontrar chão antes de desistir (evita crash)

    private float timer;

    private void Start()
    {
        if (arenaBounds == null) arenaBounds = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            TrySpawnPowerup();
            timer = 0f;
        }
    }

    private void TrySpawnPowerup()
    {
        if (powerupPrefabNames.Length == 0) return;

        Bounds bounds = arenaBounds.bounds;

        Debug.Log($"1---~ {bounds}");

        // Tenta encontrar um lugar válido X vezes
        for (int i = 0; i < maxAttempts; i++)
        {
            // 1. Escolhe um X aleatório dentro da área
            float randomX = Random.Range(bounds.min.x, bounds.max.x);

            // 2. Define o ponto de partida do raio (no topo da área, nesse X)
            Vector2 rayOrigin = new Vector2(randomX, bounds.max.y);

            // 3. Dispara o raio para BAIXO
            // O raio viaja a altura toda da caixa (bounds.size.y) à procura da layer "Ground"
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, bounds.size.y, groundMask);

            // 4. Se bateu em algo (Chão)
            if (hit.collider != null)
            {
                // Escolhe um powerup
                string randomPrefabName = powerupPrefabNames[Random.Range(0, powerupPrefabNames.Length)];

                // Calcula a posição final (Ponto de impacto + Offset para cima)
                Vector2 spawnPos = hit.point + new Vector2(0, verticalOffset);

                // Cria o objeto
                PhotonNetwork.InstantiateRoomObject(randomPrefabName, spawnPos, Quaternion.identity);

                Debug.Log($"Powerup spawnado em: {spawnPos} (Tentativa {i + 1})");
                return; // Sucesso! Sai da função.
            }
        }

        Debug.LogWarning("Não foi possível encontrar chão para o powerup após várias tentativas.");
    }

    // Desenha o raio na Scene View para veres onde ele está a procurar (Debug visual)
    private void OnDrawGizmosSelected()
    {
        if (arenaBounds != null)
        {
            Gizmos.color = Color.yellow;
            Vector2 top = new Vector2(arenaBounds.bounds.center.x, arenaBounds.bounds.max.y);
            Vector2 bottom = new Vector2(arenaBounds.bounds.center.x, arenaBounds.bounds.min.y);
            Gizmos.DrawLine(top, bottom);
        }
    }
}