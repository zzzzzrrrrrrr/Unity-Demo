// Path: Assets/_Scripts/Core/ActorStats.cs
using System.Collections;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Core
{
 
    public class ActorStats : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Unique actor id used by HUD, hit events and trigger bindings.")]
        [SerializeField] private string actorId = string.Empty;
        [SerializeField] private ActorTeam team = ActorTeam.Neutral;
        [SerializeField] private bool autoGenerateActorId = true;

        [Header("Vitals")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxMana = 50f;
        [SerializeField] private float maxArmor = 0f;

        [Header("Mana Regen")]
        [SerializeField] private bool enableManaRegen = true;
        [SerializeField] private float manaRegenPerSecond = 4f;
        [SerializeField] private float manaRegenDelayAfterConsume = 1f;

        [Header("Combat Stats")]
        [SerializeField] private float attackPower = 20f;
        [SerializeField] private float defensePower = 5f;
        [SerializeField] private float criticalChance = 0.15f;
        [SerializeField] private float criticalMultiplier = 1.6f;
        [SerializeField] private int minimumDamage = 1;

        [Header("Damage Rules")]
        [Tooltip("Temporary invincible time after being hit, in seconds.")]
        [SerializeField] private float hurtInvincibleDuration = 0.2f;
        [SerializeField] private float defenseScale = 1f;

        [Header("Death")]
        [SerializeField] private bool destroyOnDeath = false;
        [SerializeField] private float destroyDelay = 1f;
        [SerializeField] private bool autoHideEnemyCorpse = true;
        [SerializeField] private float enemyCorpseHideDelay = 0.9f;
        [SerializeField] private bool disablePhysicsOnDeath = true;

        [Header("Visual Feedback")]
        [SerializeField] private bool enableHitFlash = true;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float hitFlashDuration = 0.06f;
        [SerializeField] private bool enableDeathFade = true;
        [SerializeField] private float deathFadeDuration = 0.4f;
        [SerializeField] private bool disableRenderersAfterFade = true;

        [Header("Debug")]
        [SerializeField] private bool enableCombatDebugLog = false;

        private float currentHealth;
        private float currentMana;
        private float currentArmor;

        private bool isDead;

        private float invincibleUntilTime;
        private float nextManaRegenTime;

        private bool initialized;
        private SpriteRenderer[] cachedRenderers = new SpriteRenderer[0];
        private Color[] cachedRendererColors = new Color[0];
        private Coroutine hitFlashRoutine;
        private Coroutine deathFadeRoutine;
        private Coroutine corpseCleanupRoutine;

        public string ActorId => actorId;
        public ActorTeam Team => team;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float MaxMana => maxMana;
        public float CurrentMana => currentMana;
        public float MaxArmor => maxArmor;
        public float CurrentArmor => currentArmor;
        public float AttackPower => attackPower;
        public float DefensePower => defensePower;
        public float CriticalChance => criticalChance;
        public float CriticalMultiplier => criticalMultiplier;
        public int MinimumDamage => minimumDamage;
        public bool IsDead => isDead;


        public bool IsInvincible => Time.time < invincibleUntilTime;

        private void Awake()
        {
            InitializeRuntimeData();
            CacheRenderers();
            RestoreVisualState();
        }

        private void OnEnable()
        {
            InitializeRuntimeData();
            CacheRenderers();
            if (!isDead)
            {
                RestoreVisualState();
            }
            BroadcastVitals();
        }

        private void Update()
        {
            TickManaRegen();
        }

        private void OnDisable()
        {
            if (hitFlashRoutine != null)
            {
                StopCoroutine(hitFlashRoutine);
                hitFlashRoutine = null;
            }

            if (deathFadeRoutine != null)
            {
                StopCoroutine(deathFadeRoutine);
                deathFadeRoutine = null;
            }

            if (corpseCleanupRoutine != null)
            {
                StopCoroutine(corpseCleanupRoutine);
                corpseCleanupRoutine = null;
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            maxMana = Mathf.Max(0f, maxMana);
            maxArmor = Mathf.Max(0f, maxArmor);
            attackPower = Mathf.Max(0f, attackPower);
            defensePower = Mathf.Max(0f, defensePower);
            criticalChance = Mathf.Clamp01(criticalChance);
            criticalMultiplier = Mathf.Max(1f, criticalMultiplier);
            minimumDamage = Mathf.Max(1, minimumDamage);
            manaRegenPerSecond = Mathf.Max(0f, manaRegenPerSecond);
            manaRegenDelayAfterConsume = Mathf.Max(0f, manaRegenDelayAfterConsume);
            hurtInvincibleDuration = Mathf.Max(0f, hurtInvincibleDuration);
            defenseScale = Mathf.Max(0f, defenseScale);
            destroyDelay = Mathf.Max(0f, destroyDelay);
            enemyCorpseHideDelay = Mathf.Max(0f, enemyCorpseHideDelay);
            hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
            deathFadeDuration = Mathf.Max(0f, deathFadeDuration);
        }

    
        private void InitializeRuntimeData()
        {
            if (initialized)
            {
                return;
            }

            if (autoGenerateActorId && string.IsNullOrEmpty(actorId))
            {
   
                actorId = team == ActorTeam.Player ? "Player" : $"{team}_{GetInstanceID()}";
            }

            currentHealth = maxHealth;
            currentMana = maxMana;
            currentArmor = maxArmor;
            isDead = false;
            invincibleUntilTime = 0f;
            nextManaRegenTime = 0f;
            initialized = true;
        }

        public void BroadcastVitals()
        {
            EventCenter.Broadcast(new ActorHealthChangedEvent(
                actorId,
                team,
                currentHealth,
                maxHealth,
                currentMana,
                maxMana,
                isDead,
                transform.position));
        }

    
        public void BroadcastState(ActorStateType state)
        {
            EventCenter.Broadcast(new ActorStateChangedEvent(actorId, team, state));
        }

         public bool TryConsumeMana(float amount)
        {
            if (isDead)
            {
                return false;
            }

            if (amount <= 0f)
            {
                return true;
            }

            if (currentMana < amount)
            {
                return false;
            }

            currentMana -= amount;
            nextManaRegenTime = Time.time + manaRegenDelayAfterConsume;
            BroadcastVitals();
            return true;
        }

        public void RecoverMana(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            currentMana = Mathf.Clamp(currentMana + amount, 0f, maxMana);
            BroadcastVitals();
        }

  
        public void AddTemporaryInvincible(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            invincibleUntilTime = Mathf.Max(invincibleUntilTime, Time.time + duration);
        }

        private void TickManaRegen()
        {
            if (isDead || !enableManaRegen || currentMana >= maxMana || manaRegenPerSecond <= 0f)
            {
                return;
            }

            if (Time.time < nextManaRegenTime)
            {
                return;
            }

            float regenAmount = manaRegenPerSecond * Time.deltaTime;
            if (regenAmount <= 0f)
            {
                return;
            }

            float before = currentMana;
            currentMana = Mathf.Clamp(currentMana + regenAmount, 0f, maxMana);
            if (!Mathf.Approximately(before, currentMana))
            {
                BroadcastVitals();
            }
        }

        public int TakeDamage(
            ActorStats attacker,
            float skillMultiplier,
            float flatBonus,
            Vector3 hitPosition,
            bool ignoreInvincible,
            out bool isCritical)
        {
            isCritical = false;

    
            if (isDead)
            {
                return 0;
            }

      
            if (!ignoreInvincible && IsInvincible)
            {
                return 0;
            }

          
            DamageContext context = new DamageContext
            {
                AttackerAttack = attacker != null ? attacker.AttackPower : 0f,
                DefenderDefense = defensePower,
                SkillMultiplier = Mathf.Max(0f, skillMultiplier),
                FlatBonus = flatBonus,
                DefenseScale = defenseScale,
                CriticalChance = attacker != null ? attacker.CriticalChance : 0f,
                CriticalMultiplier = attacker != null ? attacker.CriticalMultiplier : 1f,
                MinimumDamage = attacker != null ? attacker.MinimumDamage : minimumDamage
            };

            int finalDamage = BattleFormula.CalculateDamage(context, out isCritical);

            float armorBeforeDamage = currentArmor;
            float remainDamage = finalDamage;
            if (remainDamage > 0f && currentArmor > 0f)
            {
                float absorb = Mathf.Min(currentArmor, remainDamage);
                currentArmor -= absorb;
                remainDamage -= absorb;
            }

            currentArmor = Mathf.Max(0f, currentArmor);
            currentHealth = Mathf.Max(0f, currentHealth - remainDamage);

            if (finalDamage > 0)
            {
                PlayHitFlash();
            }


            if (enableCombatDebugLog)
            {
                string attackerId = attacker != null ? attacker.ActorId : "Unknown";
                Debug.Log("[Combat][Hit] Attacker=" + attackerId + ", Target=" + actorId + ", Damage=" + finalDamage + ", HP=" + currentHealth + "/" + maxHealth);
            }

          
            if (!ignoreInvincible)
            {
                AddTemporaryInvincible(hurtInvincibleDuration);
            }


            EventCenter.Broadcast(new DamageAppliedEvent(
                attacker != null ? attacker.ActorId : string.Empty,
                actorId,
                finalDamage,
                isCritical,
                attacker != null ? attacker.transform.position : transform.position,
                hitPosition));

            if (armorBeforeDamage > 0f && currentArmor <= 0f)
            {
                EventCenter.Broadcast(new ArmorBrokenEvent(actorId, team, transform.position));
            }

            BroadcastVitals();

            if (currentHealth <= 0f)
            {
                Die();
            }
            else
            {
                BroadcastState(ActorStateType.Hurt);
            }

            return finalDamage;
        }


        public void RecoverHealth(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
            BroadcastVitals();
        }

        public void Revive(float hpPercent, float mpPercent)
        {
            isDead = false;

            float hp = Mathf.Clamp01(hpPercent) * maxHealth;
            float mp = Mathf.Clamp01(mpPercent) * maxMana;


            currentHealth = Mathf.Max(1f, hp);
            currentMana = mp;
            RestoreVisualState();

            BroadcastVitals();
            BroadcastState(ActorStateType.Idle);
            EventCenter.Broadcast(new ActorRevivedEvent(actorId, team, transform.position));
        }

        public void ForceSetRuntimeValues(float hp, float mp, bool dead)
        {
            currentHealth = Mathf.Clamp(hp, 0f, maxHealth);
            currentMana = Mathf.Clamp(mp, 0f, maxMana);
            isDead = dead;

            if (isDead && currentHealth > 0f)
            {
                currentHealth = 0f;
            }
            else if (!isDead)
            {
                RestoreVisualState();
            }

            BroadcastVitals();
            BroadcastState(isDead ? ActorStateType.Death : ActorStateType.Idle);
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentHealth = 0f;

            if (enableCombatDebugLog)
            {
                Debug.Log("[Combat][Death] Target=" + actorId + ", HP=0/" + maxHealth);
            }

            BroadcastVitals();
            BroadcastState(ActorStateType.Death);
            EventCenter.Broadcast(new ActorDiedEvent(actorId, team, transform.position));

            if (disablePhysicsOnDeath && team == ActorTeam.Enemy)
            {
                DisablePhysicsAndColliders();
            }

            if (enableDeathFade)
            {
                if (deathFadeRoutine != null)
                {
                    StopCoroutine(deathFadeRoutine);
                }

                deathFadeRoutine = StartCoroutine(CoDeathFade());
            }

            if (destroyOnDeath)
            {
                Destroy(gameObject, destroyDelay);
                return;
            }

            if (team == ActorTeam.Enemy && autoHideEnemyCorpse)
            {
                if (corpseCleanupRoutine != null)
                {
                    StopCoroutine(corpseCleanupRoutine);
                }

                corpseCleanupRoutine = StartCoroutine(CoHideEnemyCorpseAfterDelay());
            }
        }

        private void DisablePhysicsAndColliders()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private void CacheRenderers()
        {
            cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            cachedRendererColors = new Color[cachedRenderers.Length];
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                SpriteRenderer sr = cachedRenderers[i];
                cachedRendererColors[i] = sr != null ? sr.color : Color.white;
            }
        }

        private void RestoreVisualState()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheRenderers();
            }

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                SpriteRenderer sr = cachedRenderers[i];
                if (sr == null)
                {
                    continue;
                }

                sr.enabled = true;
                Color baseColor = i < cachedRendererColors.Length ? cachedRendererColors[i] : sr.color;
                baseColor.a = 1f;
                sr.color = baseColor;
            }
        }

        private void PlayHitFlash()
        {
            if (!enableHitFlash || hitFlashDuration <= 0f)
            {
                return;
            }

            if (hitFlashRoutine != null)
            {
                StopCoroutine(hitFlashRoutine);
            }

            hitFlashRoutine = StartCoroutine(CoHitFlash());
        }

        private IEnumerator CoHitFlash()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheRenderers();
            }

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].color = hitFlashColor;
                }
            }

            yield return new WaitForSeconds(hitFlashDuration);

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                SpriteRenderer sr = cachedRenderers[i];
                if (sr == null || !sr.enabled)
                {
                    continue;
                }

                Color color = i < cachedRendererColors.Length ? cachedRendererColors[i] : sr.color;
                if (isDead)
                {
                    color.a = sr.color.a;
                }
                else
                {
                    color.a = 1f;
                }

                sr.color = color;
            }

            hitFlashRoutine = null;
        }

        private IEnumerator CoDeathFade()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheRenderers();
            }

            if (cachedRenderers.Length == 0)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, deathFadeDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(1f, 0f, t);

                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    SpriteRenderer sr = cachedRenderers[i];
                    if (sr == null)
                    {
                        continue;
                    }

                    Color color = i < cachedRendererColors.Length ? cachedRendererColors[i] : sr.color;
                    color.a = alpha;
                    sr.color = color;
                }

                yield return null;
            }

            if (disableRenderersAfterFade)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null)
                    {
                        cachedRenderers[i].enabled = false;
                    }
                }
            }

            deathFadeRoutine = null;
        }

        private IEnumerator CoHideEnemyCorpseAfterDelay()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, enemyCorpseHideDelay));
            gameObject.SetActive(false);
            corpseCleanupRoutine = null;
        }
    }
}
