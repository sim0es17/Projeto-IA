using System.Collections;
using UnityEngine;

public class HealthPowerupSpawner : MonoBehaviour
{
    [Header("Referências")]
    public GameObject powerupPrefab;   // prefab do coração
    public Collider2D arenaBounds;     // o BoxCollider2D do ArenaBounds
    public LayerMask groundMask;       // só a layer Ground

    [Header("Tempo")]
    public float tempoNoMapa = 10f;       // tempo que o powerup fica activo
    public float tempoEntreSpawns = 2f;   // pausa entre spawns

    [Header("Outros")]
    public float offsetY = 0.5f;       // sobe um bocadinho acima do chão
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

        // liga o powerup a este spawner
        HealthPowerup hp = powerupAtual.GetComponent<HealthPowerup>();
        if (hp != null)
            hp.spawner = this;

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
                yield break; // já foi apanhado

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
        Bounds b = arenaBounds.bounds;

        float minX = b.min.x;
        float maxX = b.max.x;
        float startY = b.max.y + 2f;

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

        Debug.LogWarning("HealthPowerupSpawner: não encontrei chão, a usar centro dos bounds.");
        return b.center;
    }
}
