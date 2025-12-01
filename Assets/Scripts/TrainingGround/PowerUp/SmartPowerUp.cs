using UnityEngine;
using System.Collections;

public class SmartPowerUp : MonoBehaviour
{
    public enum EffectType
    {
        Heal,
        DamageBoost
    }

    [Header("Decisão")]
    [Range(0f, 1f)]
    public float lowHealthThreshold = 0.3f;   // 30% de vida para decidir cura

    [Header("Efeitos")]
    public float effectDuration = 15f;        // Duração do buff de dano
    public float damageMultiplier = 1.5f;     // +50% de dano

    private bool consumed = false;

    // Para esconder o power up ao ser apanhado
    private Collider2D col;
    private SpriteRenderer sr;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (consumed) return;

        if (!collision.CompareTag("Player"))
            return;

        // Health é obrigatório
        Health playerHealth = collision.GetComponentInParent<Health>();
        if (playerHealth == null)
        {
            Debug.LogWarning("SMART POWER-UP: Player sem componente Health.");
            return;
        }

        // Combat é opcional (só para o buff de dano)
        CombatSystem2D combat = collision.GetComponentInParent<CombatSystem2D>();

        float healthPercent = (float)playerHealth.health / playerHealth.maxHealth;
        Debug.Log($"SMART POWER-UP: vida actual = {healthPercent * 100f:0}%");

        // 1 — decidir efeito
        EffectType chosen = DecideEffect(playerHealth, combat);
        Debug.Log($"SMART POWER-UP: efeito escolhido = {chosen}");

        // 2 — aplicar efeito
        ApplyEffect(chosen, playerHealth, combat);

        consumed = true;
    }

    private EffectType DecideEffect(Health playerHealth, CombatSystem2D combat)
    {
        float healthPercent = (float)playerHealth.health / playerHealth.maxHealth;

        // Vida baixa ? cura
        if (healthPercent <= lowHealthThreshold)
            return EffectType.Heal;

        // Sem CombatSystem2D não conseguimos buff ? cura em vez disso
        if (combat == null)
            return EffectType.Heal;

        // Caso normal ? buff de dano
        return EffectType.DamageBoost;
    }

    private void ApplyEffect(EffectType effect, Health playerHealth, CombatSystem2D combat)
    {
        switch (effect)
        {
            case EffectType.Heal:
                HealToFull(playerHealth);
                HideVisuals();          // some logo
                Destroy(gameObject);    // não precisa de ficar vivo
                break;

            case EffectType.DamageBoost:
                if (combat != null)
                {
                    HideVisuals();                      // some logo ao apanhar
                    StartCoroutine(DamageBoostRoutine(combat));
                }
                else
                {
                    // fallback de segurança
                    HealToFull(playerHealth);
                    HideVisuals();
                    Destroy(gameObject);
                }
                break;
        }
    }

    private void HealToFull(Health playerHealth)
    {
        int amountToFull = playerHealth.maxHealth - playerHealth.health;
        if (amountToFull > 0)
        {
            playerHealth.Heal(amountToFull);
            Debug.Log("SMART POWER-UP: Cura total aplicada.");
        }
        else
        {
            Debug.Log("SMART POWER-UP: Vida já estava cheia.");
        }
    }

    private IEnumerator DamageBoostRoutine(CombatSystem2D combat)
    {
        int originalDamage = combat.damage;
        int boostedDamage = Mathf.RoundToInt(originalDamage * damageMultiplier);

        combat.damage = boostedDamage;
        Debug.Log($"SMART POWER-UP: Dano aumentado para {boostedDamage} durante {effectDuration}s.");

        yield return new WaitForSeconds(effectDuration);

        combat.damage = originalDamage;
        Debug.Log("SMART POWER-UP: Buff terminou, dano voltou ao normal.");

        // Agora podemos destruir o objecto invisível
        Destroy(gameObject);
    }

    private void HideVisuals()
    {
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;
    }
}
