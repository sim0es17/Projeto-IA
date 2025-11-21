using System.Collections;
using UnityEngine;

public class SpeedJumpPowerupSpawner : MonoBehaviour
{
    [Header("Referências")]
    public GameObject powerupPrefab;   // Prefab do ícone de velocidade/pulo
    public Collider2D arenaBounds;     // O BoxCollider2D da Arena
    public LayerMask groundMask;       // Layer do chão

    [Header("Tempo")]
    public float tempoNoMapa = 10f;       // Tempo que o item fica no chão antes de sumir
    public float tempoEntreSpawns = 5f;   // Tempo para reaparecer depois de apanhado

    [Header("Outros")]
    public float offsetY = 0.5f;       
    public int maxTentativas = 20;

    private GameObject powerupAtual;
    private Coroutine lifetimeRoutine;

    private void Start()
    {
        SpawnNovoPowerup();
    }

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

        Vector2 pos = GetPosicaoAleatoriaNoChao();
        powerupAtual = Instantiate(powerupPrefab, pos, Quaternion.identity);

        // Liga o powerup a este spawner
        SpeedJumpPowerup sp = powerupAtual.GetComponent<SpeedJumpPowerup>();
        if (sp != null)
            sp.spawner = this;

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
                yield break; // Já foi apanhado

            tempo -= Time.deltaTime;
            yield return null;
        }

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
            Debug.LogError("ArenaBounds não definido no Spawner!");
            return Vector2.zero;
        }

        Bounds b = arenaBounds.bounds;

        float minX = b.min.x;
        float maxX = b.max.x;
        float startY = b.max.y + 2f; // Começa o raio de cima dos limites

        for (int i = 0; i < maxTentativas; i++)
        {
            float x = Random.Range(minX, maxX);
            Vector2 origem = new Vector2(x, startY);

            RaycastHit2D hit = Physics2D.Raycast(
                origem,
                Vector2.down,
                b.size.y + 10f,
                groundMask
            );

            if (hit.collider != null)
            {
                return hit.point + Vector2.up * offsetY;
            }
        }

        Debug.LogWarning("SpeedSpawner: não encontrei chão, a usar centro.");
        return b.center;
    }
}
