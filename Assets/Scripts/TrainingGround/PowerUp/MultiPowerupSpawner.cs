using System.Collections;
using UnityEngine;

// Renomeado para refletir a sua nova funcionalidade
public class MultiPowerupSpawner : MonoBehaviour
{
    [Header("Referencias")]
    // ARRAY de todos os prefabs de powerups que podem ser spawnados
    public GameObject[] powerupPrefabs; 
    public Collider2D arenaBounds; 
    public LayerMask groundMask; 

    [Header("Tempo")]
    public float tempoNoMapa = 10f; 
    public float tempoEntreSpawns = 2f; 

    [Header("Outros")]
    public float offsetY = 0.5f; 
    public int maxTentativas = 20;

    private GameObject powerupAtual;
    private Coroutine lifetimeRoutine;

    private void Start()
    {
        if (powerupPrefabs == null || powerupPrefabs.Length == 0)
        {
            Debug.LogError("MultiPowerupSpawner: O array 'powerupPrefabs' está vazio. Não é possível gerar Power-Ups.");
            return;
        }
        SpawnNovoPowerup();
    }

    /// <summary>
    /// Chamado pelos Power-Ups quando são apanhados.
    /// </summary>
    public void PowerupApanhado()
    {
        if (lifetimeRoutine != null)
            StopCoroutine(lifetimeRoutine);

        powerupAtual = null;
        StartCoroutine(RespawnDepoisDeDelay());
    }

    private IEnumerator RespawnDepoisDeDelay()
    {
        yield return new WaitForSeconds(tempoEntreSpawns);
        SpawnNovoPowerup();
    }

    private void SpawnNovoPowerup()
    {
        if (powerupAtual != null)
            Destroy(powerupAtual);
        
        // 1. ESCOLHE UM PREFAB ALEATORIAMENTE
        int randomIndex = Random.Range(0, powerupPrefabs.Length);
        GameObject selectedPrefab = powerupPrefabs[randomIndex];

        Vector2 pos = GetPosicaoAleatoriaNoChao();
        powerupAtual = Instantiate(selectedPrefab, pos, Quaternion.identity);

        // 2. LIGA O POWER-UP AO ESTE SPAWNER
        // Usamos GetComponents<IRespawnable> se tivéssemos interfaces, 
        // mas vamos usar a abordagem mais direta para os seus scripts existentes:
        
        // A. Verifica HealthPowerup (se existir)
        HealthPowerup hp = powerupAtual.GetComponent<HealthPowerup>();
        if (hp != null)
            hp.spawner = this;

        // B. Verifica SpeedJumpPowerup (se existir)
        SpeedJumpPowerup sjp = powerupAtual.GetComponent<SpeedJumpPowerup>();
        if (sjp != null)
            sjp.spawner = this;

        // C. Verifica SmartPowerUp (se existir)
        SmartPowerUp smartp = powerupAtual.GetComponent<SmartPowerUp>();
        if (smartp != null)
            smartp.spawner = this;

        // 3. INICIA O TIMER DE VIDA
        if (lifetimeRoutine != null)
            StopCoroutine(lifetimeRoutine);

        lifetimeRoutine = StartCoroutine(TimerDeVida(powerupAtual));
    }

    private IEnumerator TimerDeVida(GameObject estePowerup)
    {
        float tempo = tempoNoMapa;

        while (tempo > 0f)
        {
            if (estePowerup == null)
                yield break; // Já foi apanhado (o PowerupApanhado() foi chamado)

            tempo -= Time.deltaTime;
            yield return null;
        }

        // Tempo esgotado
        if (estePowerup != null)
        {
            Destroy(estePowerup);
            powerupAtual = null;
            StartCoroutine(RespawnDepoisDeDelay());
        }
    }

    private Vector2 GetPosicaoAleatoriaNoChao()
    {
        if (arenaBounds == null)
        {
            Debug.LogError("ArenaBounds não atribuído. Usando (0,0).");
            return Vector2.zero;
        }

        Bounds b = arenaBounds.bounds;

        float minX = b.min.x;
        float maxX = b.max.x;
        float startY = b.max.y + 2f; // Começa um pouco acima do limite superior

        for (int i = 0; i < maxTentativas; i++)
        {
            float x = Random.Range(minX, maxX);
            Vector2 origem = new Vector2(x, startY);

            // Tenta encontrar o chão
            RaycastHit2D hit = Physics2D.Raycast(
                origem,
                Vector2.down,
                b.size.y + 10f, // Distância grande o suficiente
                groundMask
            );

            if (hit.collider != null)
            {
                // Ponto encontrado no chão, sobe um pouco
                return hit.point + Vector2.up * offsetY;
            }
        }

        Debug.LogWarning("MultiPowerupSpawner: Não encontrei chão após " + maxTentativas + " tentativas. A usar centro dos bounds.");
        return b.center;
    }
}
