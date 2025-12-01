using UnityEngine;
using System.Collections;

public class SmartPowerUp : MonoBehaviour
{
    // --- ENUM e Configuração ---
    public enum EffectType
    {
        Heal,
        DamageBoost,
        SpeedBoost
    }

    [Header("Referencias do Spawner")]
    [HideInInspector]
    public MultiPowerupSpawner spawner; // Referência para notificar o spawner (CORRIGE CS1061)

    [Header("Decisao")]
    [Range(0f, 1f)]
    public float lowHealthThreshold = 0.3f; // 30% de vida para decidir cura
    public float enemyCheckRadius = 5f; // Raio para verificar inimigos (Dano vs Velocidade)

    [Header("Efeitos - Dano")]
    public float damageDuration = 15f;
    public float damageMultiplier = 1.5f;

    [Header("Efeitos - Velocidade")]
    public float speedDuration = 15f;
    public float speedMultiplier = 1.5f;
    public float jumpMultiplier = 1.3f;

    // --- VARIÁVEIS INTERNAS ---
    private bool consumed = false;
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

        // Tenta obter componentes do objeto pai do collider
        Health playerHealth = collision.GetComponentInParent<Health>();
        Movement2D playerMovement = collision.GetComponentInParent<Movement2D>();
        CombatSystem2D combat = collision.GetComponentInParent<CombatSystem2D>();

        // Health é obrigatório
        if (playerHealth == null) return;
        
        // Se não tiver movimento, não há como aplicar nenhum buff de velocidade ou dano (exceto cura)
        if (playerMovement == null)
        {
            Debug.LogWarning("SMART POWER-UP: Player sem componente Movement2D.");
            return;
        }

        float healthPercent = (float)playerHealth.health / playerHealth.maxHealth;
        
        // 1 - DECIDIR EFEITO
        EffectType chosen = DecideEffect(playerHealth, combat, playerMovement);
        Debug.Log($"SMART POWER-UP: Efeito escolhido = {chosen} (Vida: {healthPercent * 100f:0}%)");

        // 2 - APLICAR EFEITO
        ApplyEffect(chosen, playerHealth, combat, playerMovement);

        consumed = true;
    }

    // ------------------------------------------------------------------
    //                         MÉTODO DE DECISÃO INTELIGENTE
    // ------------------------------------------------------------------
    private EffectType DecideEffect(Health playerHealth, CombatSystem2D combat, Movement2D movement)
    {
        float healthPercent = (float)playerHealth.health / playerHealth.maxHealth;

        // 1. PRIORIDADE MÁXIMA: SOBREVIVÊNCIA
        if (healthPercent <= lowHealthThreshold)
            return EffectType.Heal;

        // 2. PRIORIDADE: CONTEXTO DE COMBATE
        if (IsEnemyNearby())
        {
            // O jogador está em combate, o DANO é mais útil
            if (combat != null)
                return EffectType.DamageBoost;
            
            // Fallback: está em perigo mas não pode lutar? Cura.
            return EffectType.Heal;
        }

        // 3. PRIORIDADE: CONTEXTO DE EXPLORAÇÃO/MOVIMENTO
        // Não há inimigos por perto, a VELOCIDADE é mais útil
        if (movement != null)
        {
            return EffectType.SpeedBoost;
        }

        // 4. FALLBACK FINAL: Cura se falhar todas as verificações de componente.
        return EffectType.Heal;
    }

    private bool IsEnemyNearby()
    {
        // Verifica se existe algum objeto na Layer "Enemy" dentro do raio 'enemyCheckRadius'.
        // ATENÇÃO: Confirme que a sua Layer de inimigos se chama "Enemy"
        LayerMask enemyLayer = LayerMask.GetMask("enemyLayers"); 
        
        Collider2D hit = Physics2D.OverlapCircle(transform.position, enemyCheckRadius, enemyLayer);
        
        return hit != null;
    }

    // ------------------------------------------------------------------
    //                         APLICAÇÃO DO EFEITO
    // ------------------------------------------------------------------
    private void ApplyEffect(EffectType effect, Health playerHealth, CombatSystem2D combat, Movement2D movement)
    {
        HideVisuals(); 

        switch (effect)
        {
            case EffectType.Heal:
                HealToFull(playerHealth);
                // Notifica o spawner e destrói
                if (spawner != null) spawner.PowerupApanhado();
                Destroy(gameObject); 
                break;

            case EffectType.DamageBoost:
                StartCoroutine(DamageBoostRoutine(combat));
                break;
            
            case EffectType.SpeedBoost: 
                movement.ActivateSpeedJumpBuff(speedMultiplier, jumpMultiplier, speedDuration);
                // Notifica o spawner e destrói
                if (spawner != null) spawner.PowerupApanhado();
                Destroy(gameObject); 
                break;
        }
    }

    // --- Rotinas de Efeitos ---

    private void HealToFull(Health playerHealth)
    {
        int amountToFull = playerHealth.maxHealth - playerHealth.health;
        if (amountToFull > 0)
        {
            playerHealth.Heal(amountToFull);
            Debug.Log("SMART POWER-UP: Cura total aplicada.");
        }
    }

    private IEnumerator DamageBoostRoutine(CombatSystem2D combat)
    {
        int originalDamage = combat.damage;
        int boostedDamage = Mathf.RoundToInt(originalDamage * damageMultiplier);

        combat.damage = boostedDamage;
        Debug.Log($"SMART POWER-UP: Dano aumentado para {boostedDamage} durante {damageDuration}s.");

        yield return new WaitForSeconds(damageDuration);

        combat.damage = originalDamage;
        Debug.Log("SMART POWER-UP: Buff terminou, dano voltou ao normal.");

        // Notifica o spawner APÓS o buff terminar
        if (spawner != null) spawner.PowerupApanhado();

        // Destrói o objeto
        Destroy(gameObject);
    }

    private void HideVisuals()
    {
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;
    }

    // Gizmo para visualizar o raio de verificação de inimigos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyCheckRadius);
    }
}
