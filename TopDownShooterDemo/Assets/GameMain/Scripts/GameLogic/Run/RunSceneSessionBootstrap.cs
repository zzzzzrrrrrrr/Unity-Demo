using System.Collections;
using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.UI;
using GameMain.GameLogic.Weapons;
using GameMain.GameLogic.World;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.Run
{
    /// <summary>
    /// Applies selected character session data and starts the RunScene vertical slice flow.
    /// Ownership contract: final startup writer for selected-role runtime stats when session state exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSceneSessionBootstrap : MonoBehaviour
    {
        private const string FormalPlayerName = "Player";
        private const string RunSceneName = "RunScene";
        private const string RunSceneLevel2Name = "RunScene_Level2";
        private const string EvidenceLogPrefix = "RunSceneFormalPlayerEvidence";
        private const bool VerboseLogging = false;
        private const string RoleNameCanvasObjectName = "RunSceneRoleNameCanvas";
        private const string RoleNameTextObjectName = "CurrentRoleNameText";
        private static Font overlayFont;
        private static Sprite fallbackPlayerSprite;

        [SerializeField] private string expectedSceneName = "RunScene";
        [SerializeField] private string returnSceneName = "CharacterSelectScene";
        [SerializeField] [Min(0.5f)] private float bootstrapTimeout = 8f;
        [SerializeField] private bool forceBattleProcedureOnStart = true;
        [SerializeField] private Vector3 runtimePlayerBaseScale = Vector3.one;

        private bool started;

        private struct PlayerPresentationSummary
        {
            public string SpriteSource;
            public string SpriteName;
            public Color Tint;
        }

        private void OnEnable()
        {
            if (started)
            {
                return;
            }

            started = true;
            if (IsSupportedRunSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            {
                BossRushRuntimeSceneBuilder.BootstrapCurrentScene();
            }

            CleanupLegacyRunUi();
            StartCoroutine(BootstrapRoutine());
        }

        private IEnumerator BootstrapRoutine()
        {
            var activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!IsSupportedRunSceneName(activeSceneName))
            {
                yield break;
            }

            var requiresFlowController = RequiresVerticalSliceFlow(activeSceneName);

            // Let scene Start() calls settle first (notably GameEntry auto Launch/Menu),
            // then enforce RunScene battle bootstrap so gate lock state does not get reverted.
            yield return null;

            CleanupLegacyRunUi();

            var elapsed = 0f;
            ProcedureManager procedureManager = null;
            PlayerHealth playerHealth = null;
            PlayerController playerController = null;
            WeaponController playerWeapon = null;
            VerticalSliceFlowController flowController = null;
            while (elapsed < bootstrapTimeout)
            {
                ResolveRunReferences(
                    ref procedureManager,
                    ref playerHealth,
                    ref playerController,
                    ref playerWeapon,
                    ref flowController);
                if (procedureManager != null && (!requiresFlowController || flowController != null))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (procedureManager == null || (requiresFlowController && flowController == null))
            {
                Debug.LogWarning("RunSceneSessionBootstrap aborted: required references were not found before timeout.", this);
                yield break;
            }

            var formalPlayerSpawnPoint = ResolveFormalPlayerSpawnPoint();
            playerHealth = ResolveFormalPlayerHealth(playerHealth);
            if (playerHealth == null)
            {
                playerHealth = CreateFormalPlayer(formalPlayerSpawnPoint);
                if (playerHealth == null)
                {
                    Debug.LogWarning("RunSceneSessionBootstrap aborted: cannot create formal player object.", this);
                    yield break;
                }
            }

            playerHealth = EnsureFormalPlayerIdentity(
                playerHealth,
                formalPlayerSpawnPoint,
                ref playerController,
                ref playerWeapon);
            if (playerHealth == null || playerController == null || playerWeapon == null)
            {
                Debug.LogWarning("RunSceneSessionBootstrap aborted: formal player chain is incomplete.", this);
                yield break;
            }

            EnforceSinglePlayerInstance(playerHealth);
            EnforceSingleRuntimeStateAttachment(playerHealth);
            BindFormalPlayerChain(playerHealth, flowController);
            EnsurePlayerFirePointBinding(playerHealth, playerWeapon);
            // Selected role startup state is finalized here before downstream systems read player runtime values.
            var presentationSummary = ApplySessionCharacter(playerHealth, playerController, playerWeapon);
            BindDownstreamPlayerReferences(playerHealth, procedureManager, flowController);
            // VerticalSliceFlowController remains the run gate/flow authority.
            flowController?.SetRoleConfirmed(true);
            EnsureRoleNameOverlay(RunSessionContext.SelectedCharacterData);
            LogFormalPlayerEvidence(playerHealth, flowController, presentationSummary);

            var resultReturn = GetOrAddComponent<RunSceneResultReturnController>(gameObject);
            resultReturn.Configure(procedureManager, returnSceneName);

            if (forceBattleProcedureOnStart && procedureManager.CurrentProcedureType != ProcedureType.Battle)
            {
                procedureManager.ChangeProcedure(ProcedureType.Battle);
            }

            CleanupLegacyRunUi();
        }

        private bool IsSupportedRunSceneName(string sceneName)
        {
            if (string.Equals(sceneName, expectedSceneName, System.StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(sceneName, RunSceneName, System.StringComparison.Ordinal) ||
                   string.Equals(sceneName, RunSceneLevel2Name, System.StringComparison.Ordinal);
        }

        private static bool RequiresVerticalSliceFlow(string sceneName)
        {
            return !string.Equals(sceneName, RunSceneLevel2Name, System.StringComparison.Ordinal);
        }

        private static void ResolveRunReferences(
            ref ProcedureManager procedureManager,
            ref PlayerHealth playerHealth,
            ref PlayerController playerController,
            ref WeaponController playerWeapon,
            ref VerticalSliceFlowController flowController)
        {
            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }

            if (procedureManager == null)
            {
                procedureManager = FindObjectOfType<ProcedureManager>();
            }

            if (flowController == null)
            {
                flowController = FindObjectOfType<VerticalSliceFlowController>();
            }

            if (playerHealth == null && RuntimeSceneHooks.Active != null && RuntimeSceneHooks.Active.PlayerHealth != null)
            {
                playerHealth = RuntimeSceneHooks.Active.PlayerHealth;
            }

            if (playerHealth == null)
            {
                playerHealth = ResolvePrimaryPlayerHealth();
            }

            if (playerHealth == null)
            {
                playerHealth = FindObjectOfType<PlayerHealth>();
            }

            if (playerController == null && playerHealth != null)
            {
                playerController = playerHealth.GetComponent<PlayerController>();
            }

            if (playerWeapon == null && playerHealth != null)
            {
                playerWeapon = playerHealth.GetComponent<WeaponController>();
            }
        }

        private PlayerPresentationSummary ApplySessionCharacter(
            PlayerHealth playerHealth,
            PlayerController playerController,
            WeaponController playerWeapon)
        {
            var selectedCharacter = RunSessionContext.SelectedCharacterData;
            var selectedActorSprite = RunSessionContext.SelectedCharacterSprite;
            var runtimeState = GetOrAddComponent<RunCharacterRuntimeState>(playerHealth.gameObject);
            runtimeState.Apply(selectedCharacter, selectedActorSprite);
            var roleSkillController = GetOrAddComponent<PlayerRoleSkillController>(playerHealth.gameObject);
            roleSkillController.Bind(playerHealth, playerController);
            roleSkillController.Configure(selectedCharacter);
            var presentationSummary = ApplyCharacterPresentation(playerHealth, selectedCharacter, selectedActorSprite);
            RefreshPlayerVisualFeedbackBaselines(playerHealth.gameObject);

            if (selectedCharacter == null)
            {
                Debug.LogWarning("RunSceneSessionBootstrap: no selected character in RunSessionContext, using existing defaults.");
                return presentationSummary;
            }

            var redHealth = Mathf.Max(1f, selectedCharacter.redHealth);
            var blueArmor = Mathf.Max(0f, selectedCharacter.blueArmor);
            var energy = Mathf.Max(0f, selectedCharacter.energy);
            playerHealth.SetMaxHealth(redHealth, true);
            playerHealth.SetMaxArmor(blueArmor, true);
            playerHealth.SetMaxEnergy(energy, true);
            playerHealth.ResetHealth();

            playerController.ConfigureDodge(
                selectedCharacter.dodgeKey,
                selectedCharacter.dodgeDistance,
                selectedCharacter.dodgeDuration,
                selectedCharacter.dodgeCooldown,
                selectedCharacter.dodgeInvulnerable,
                selectedCharacter.dodgeDamageReduction);

            var weapon1Interval = ResolveWeaponValue(selectedCharacter.weapon1FireInterval, selectedCharacter.initialWeapon1, WeaponStatType.Interval);
            var weapon1Speed = ResolveWeaponValue(selectedCharacter.weapon1ProjectileSpeed, selectedCharacter.initialWeapon1, WeaponStatType.Speed);
            var weapon1Damage = ResolveWeaponValue(selectedCharacter.weapon1ProjectileDamage, selectedCharacter.initialWeapon1, WeaponStatType.Damage);
            var weapon1Lifetime = ResolveWeaponValue(selectedCharacter.weapon1ProjectileLifetime, selectedCharacter.initialWeapon1, WeaponStatType.Lifetime);
            var weapon2Interval = ResolveWeaponValue(selectedCharacter.weapon2FireInterval, selectedCharacter.initialWeapon2, WeaponStatType.Interval);
            var weapon2Speed = ResolveWeaponValue(selectedCharacter.weapon2ProjectileSpeed, selectedCharacter.initialWeapon2, WeaponStatType.Speed);
            var weapon2Damage = ResolveWeaponValue(selectedCharacter.weapon2ProjectileDamage, selectedCharacter.initialWeapon2, WeaponStatType.Damage);
            var weapon2Lifetime = ResolveWeaponValue(selectedCharacter.weapon2ProjectileLifetime, selectedCharacter.initialWeapon2, WeaponStatType.Lifetime);

            playerController.SetWeaponController(playerWeapon);
            playerController.ConfigureWeaponSlots(
                selectedCharacter.initialWeapon1,
                weapon1Interval,
                weapon1Speed,
                weapon1Damage,
                weapon1Lifetime,
                selectedCharacter.initialWeapon2,
                weapon2Interval,
                weapon2Speed,
                weapon2Damage,
                weapon2Lifetime,
                0);

            if (VerboseLogging)
            {
                Debug.Log(
                    "RunSession character applied. " +
                    "id=" + selectedCharacter.characterId +
                    " name=" + selectedCharacter.characterName +
                    " hp=" + selectedCharacter.redHealth +
                    " armor=" + selectedCharacter.blueArmor +
                    " energy=" + selectedCharacter.energy +
                    " skill=" + selectedCharacter.skillName +
                    " weapon1=" + selectedCharacter.initialWeapon1 +
                    " weapon2=" + selectedCharacter.initialWeapon2,
                    playerHealth);
            }

            return presentationSummary;
        }

        private static void EnsureRoleNameOverlay(CharacterData selectedCharacter)
        {
            var canvasObject = GameObject.Find(RoleNameCanvasObjectName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(RoleNameCanvasObjectName);
            }

            var canvas = GetOrAddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            GetOrAddComponent<GraphicRaycaster>(canvasObject);

            var scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textObject = FindOrCreateUiChild(canvasObject.transform, RoleNameTextObjectName);
            var rect = GetOrAddComponent<RectTransform>(textObject);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -22f);
            rect.sizeDelta = new Vector2(760f, 58f);

            var text = GetOrAddComponent<Text>(textObject);
            text.font = GetOverlayFont();
            text.fontSize = 30;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.93f, 0.97f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = selectedCharacter != null
                ? "Current Role: " + selectedCharacter.characterName
                : "Current Role: <No Selection>";
        }

        private PlayerPresentationSummary ApplyCharacterPresentation(PlayerHealth playerHealth, CharacterData selectedCharacter, Sprite selectedActorSprite)
        {
            if (playerHealth == null)
            {
                return new PlayerPresentationSummary
                {
                    SpriteSource = "missing_player",
                    SpriteName = "null",
                    Tint = Color.white,
                };
            }

            var renderer = ResolvePlayerRenderer(playerHealth.gameObject);
            if (renderer == null)
            {
                Debug.LogWarning("RunSceneSessionBootstrap: cannot apply character presentation because player SpriteRenderer is missing.", playerHealth);
                return new PlayerPresentationSummary
                {
                    SpriteSource = "missing_renderer",
                    SpriteName = "null",
                    Tint = Color.white,
                };
            }

            renderer.enabled = true;

            Sprite candidateSprite;
            string spriteSource;
            if (selectedActorSprite != null && !IsPlaceholderSprite(selectedActorSprite))
            {
                candidateSprite = selectedActorSprite;
                spriteSource = "session_actor_sprite";
            }
            else if (selectedCharacter != null && selectedCharacter.portrait != null && !IsPlaceholderSprite(selectedCharacter.portrait))
            {
                candidateSprite = selectedCharacter.portrait;
                spriteSource = "character_data_portrait";
            }
            else
            {
                candidateSprite = GetFallbackPlayerSprite();
                spriteSource = "fallback_sprite";
            }

            if (candidateSprite != null)
            {
                renderer.sprite = candidateSprite;
            }

            var isFallbackSprite = string.Equals(spriteSource, "fallback_sprite", System.StringComparison.Ordinal) || IsPlaceholderSprite(candidateSprite);
            var color = Color.white;
            if (isFallbackSprite && selectedCharacter != null)
            {
                color = selectedCharacter.worldTint;
                if (Mathf.Abs(color.a) < 0.75f)
                {
                    color.a = 0.95f;
                }
            }

            renderer.color = color;
            if (renderer.sortingOrder < 20)
            {
                renderer.sortingOrder = 20;
            }

            EnsurePlayerScale(playerHealth.transform);

            if (VerboseLogging)
            {
                Debug.Log(
                    "RunScene player presentation applied. sprite=" +
                    (renderer.sprite != null ? renderer.sprite.name : "null") +
                    " source=" + spriteSource +
                    " tint=" + renderer.color +
                    " player=" + playerHealth.gameObject.name,
                    playerHealth);
            }

            return new PlayerPresentationSummary
            {
                SpriteSource = spriteSource,
                SpriteName = renderer.sprite != null ? renderer.sprite.name : "null",
                Tint = renderer.color,
            };
        }

        private static void RefreshPlayerVisualFeedbackBaselines(GameObject playerObject)
        {
            if (playerObject == null)
            {
                return;
            }

            var hitFlash = playerObject.GetComponent<HitFlashFeedback>();
            if (hitFlash != null)
            {
                hitFlash.CaptureCurrentAsBaseState();
            }

            var deathFade = playerObject.GetComponent<DeathFadeFeedback>();
            if (deathFade != null)
            {
                deathFade.CaptureCurrentAsBaseState();
            }
        }

        private static SpriteRenderer ResolvePlayerRenderer(GameObject playerObject)
        {
            if (playerObject == null)
            {
                return null;
            }

            var renderer = playerObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                return renderer;
            }

            return playerObject.GetComponentInChildren<SpriteRenderer>(true);
        }

        private static bool IsPlaceholderSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return true;
            }

            var name = sprite.name;
            return string.Equals(name, "Point", System.StringComparison.Ordinal) ||
                   string.Equals(name, "RuntimeWhiteSprite", System.StringComparison.Ordinal) ||
                   string.Equals(name, "CharacterSelect_WhitePixel", System.StringComparison.Ordinal) ||
                   string.Equals(name, "CharacterSelect_WhitePixelSprite", System.StringComparison.Ordinal);
        }

        private static Sprite GetFallbackPlayerSprite()
        {
            if (fallbackPlayerSprite != null)
            {
                return fallbackPlayerSprite;
            }

            fallbackPlayerSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            return fallbackPlayerSprite;
        }

        private static PlayerHealth ResolvePrimaryPlayerHealth()
        {
            var playerObject = GameObject.Find(FormalPlayerName);
            if (playerObject == null)
            {
                return null;
            }

            return playerObject.GetComponent<PlayerHealth>();
        }

        private static PlayerHealth ResolveFormalPlayerHealth(PlayerHealth fallbackPlayer)
        {
            var hooksPlayer = RuntimeSceneHooks.Active != null ? RuntimeSceneHooks.Active.PlayerHealth : null;
            if (IsValidScenePlayer(hooksPlayer))
            {
                return hooksPlayer;
            }

            if (IsValidScenePlayer(fallbackPlayer))
            {
                return fallbackPlayer;
            }

            var namedPlayer = ResolvePrimaryPlayerHealth();
            if (IsValidScenePlayer(namedPlayer))
            {
                return namedPlayer;
            }

            var candidates = Object.FindObjectsOfType<PlayerHealth>(true);
            PlayerHealth namedActive = null;
            PlayerHealth anyActive = null;
            PlayerHealth namedAny = null;
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!IsValidScenePlayer(candidate))
                {
                    continue;
                }

                if (anyActive == null && candidate.gameObject.activeInHierarchy)
                {
                    anyActive = candidate;
                }

                if (string.Equals(candidate.gameObject.name, FormalPlayerName, System.StringComparison.Ordinal))
                {
                    if (namedAny == null)
                    {
                        namedAny = candidate;
                    }

                    if (candidate.gameObject.activeInHierarchy && namedActive == null)
                    {
                        namedActive = candidate;
                    }
                }
            }

            if (namedActive != null)
            {
                return namedActive;
            }

            if (anyActive != null)
            {
                return anyActive;
            }

            if (namedAny != null)
            {
                return namedAny;
            }

            return candidates.Length > 0 ? candidates[0] : null;
        }

        private static PlayerHealth EnsureFormalPlayerIdentity(
            PlayerHealth formalPlayer,
            Transform spawnPoint,
            ref PlayerController playerController,
            ref WeaponController playerWeapon)
        {
            if (formalPlayer == null)
            {
                return null;
            }

            var playerObject = formalPlayer.gameObject;
            if (playerObject == null)
            {
                return null;
            }

            if (!playerObject.activeSelf)
            {
                playerObject.SetActive(true);
            }

            if (!string.Equals(playerObject.name, FormalPlayerName, System.StringComparison.Ordinal))
            {
                playerObject.name = FormalPlayerName;
            }

            formalPlayer = GetOrAddComponent<PlayerHealth>(playerObject);
            playerController = GetOrAddComponent<PlayerController>(playerObject);
            playerWeapon = GetOrAddComponent<WeaponController>(playerObject);
            GetOrAddComponent<RunCharacterRuntimeState>(playerObject);
            GetOrAddComponent<PlayerRoleSkillController>(playerObject);
            var body = GetOrAddComponent<Rigidbody2D>(playerObject);
            var hitCollider = GetOrAddComponent<CircleCollider2D>(playerObject);
            EnsureFormalPlayerPhysicsAndCollision(body, hitCollider);

            var renderer = GetOrAddComponent<SpriteRenderer>(playerObject);
            if (renderer.sprite == null)
            {
                var renderers = playerObject.GetComponentsInChildren<SpriteRenderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var candidate = renderers[i];
                    if (candidate != null && candidate != renderer && candidate.sprite != null)
                    {
                        renderer.sprite = candidate.sprite;
                        break;
                    }
                }
            }

            var childRenderers = playerObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < childRenderers.Length; i++)
            {
                var candidate = childRenderers[i];
                if (candidate != null && candidate != renderer)
                {
                    candidate.enabled = false;
                }
            }

            renderer.enabled = true;
            formalPlayer.enabled = true;
            formalPlayer.SetTeam(CombatTeam.Player);
            playerController.enabled = true;
            playerController.SetWeaponController(playerWeapon);
            playerController.SetAimCamera(Camera.main);
            playerWeapon.enabled = true;
            playerWeapon.SetOwnerTeam(CombatTeam.Player);
            playerWeapon.EnsureRuntimeReferences();
            MovePlayerToSpawnPoint(formalPlayer, body, spawnPoint);
            return formalPlayer;
        }

        private static PlayerHealth CreateFormalPlayer(Transform spawnPoint)
        {
            var playerObject = new GameObject(FormalPlayerName);
            playerObject.SetActive(true);
            playerObject.transform.position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            playerObject.transform.rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            playerObject.transform.localScale = Vector3.one;
            GetOrAddComponent<SpriteRenderer>(playerObject);
            var formalPlayer = GetOrAddComponent<PlayerHealth>(playerObject);
            GetOrAddComponent<PlayerController>(playerObject);
            GetOrAddComponent<WeaponController>(playerObject);
            GetOrAddComponent<RunCharacterRuntimeState>(playerObject);
            GetOrAddComponent<PlayerRoleSkillController>(playerObject);
            GetOrAddComponent<Rigidbody2D>(playerObject);
            GetOrAddComponent<CircleCollider2D>(playerObject);
            if (VerboseLogging)
            {
                Debug.Log(
                    "RunSceneSessionBootstrap created new formal player object because no existing player chain was found. " +
                    "name=" + playerObject.name +
                    " spawn=" + (spawnPoint != null ? spawnPoint.name : "fallback_zero"),
                    playerObject);
            }
            return formalPlayer;
        }

        private static void EnsureFormalPlayerPhysicsAndCollision(Rigidbody2D body, CircleCollider2D hitCollider)
        {
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.simulated = true;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (hitCollider != null)
            {
                hitCollider.enabled = true;
                hitCollider.isTrigger = false;
                if (hitCollider.radius <= 0.01f)
                {
                    hitCollider.radius = 0.45f;
                }
            }
        }

        private static void MovePlayerToSpawnPoint(PlayerHealth formalPlayer, Rigidbody2D body, Transform spawnPoint)
        {
            if (formalPlayer == null || spawnPoint == null)
            {
                return;
            }

            var playerTransform = formalPlayer.transform;
            playerTransform.position = spawnPoint.position;
            playerTransform.rotation = spawnPoint.rotation;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private static Transform ResolveFormalPlayerSpawnPoint()
        {
            Transform fallbackSpawnPoint = null;
            var allTransforms = Object.FindObjectsOfType<Transform>(true);
            for (var i = 0; i < allTransforms.Length; i++)
            {
                var item = allTransforms[i];
                if (item == null ||
                    !item.gameObject.scene.IsValid() ||
                    !string.Equals(item.name, "SpawnPoint", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (fallbackSpawnPoint == null)
                {
                    fallbackSpawnPoint = item;
                }

                if (HasAncestorNamed(item, "StartArea"))
                {
                    return item;
                }
            }

            return fallbackSpawnPoint;
        }

        private static bool HasAncestorNamed(Transform target, string ancestorName)
        {
            if (target == null || string.IsNullOrWhiteSpace(ancestorName))
            {
                return false;
            }

            var current = target.parent;
            while (current != null)
            {
                if (string.Equals(current.name, ancestorName, System.StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsValidScenePlayer(PlayerHealth player)
        {
            return player != null && player.gameObject != null && player.gameObject.scene.IsValid();
        }

        private static void BindFormalPlayerChain(PlayerHealth formalPlayer, VerticalSliceFlowController flowController)
        {
            if (formalPlayer == null)
            {
                return;
            }

            if (RuntimeSceneHooks.Active != null)
            {
                RuntimeSceneHooks.Active.BindFormalPlayer(formalPlayer);
            }

            flowController?.BindFormalPlayer(formalPlayer);
        }

        private static void BindDownstreamPlayerReferences(
            PlayerHealth formalPlayer,
            ProcedureManager procedureManager,
            VerticalSliceFlowController flowController)
        {
            if (formalPlayer == null)
            {
                return;
            }

            var hooks = RuntimeSceneHooks.Active;
            var bossHealth = hooks != null ? hooks.BossHealth : null;
            if (bossHealth == null)
            {
                var allBossHealth = Object.FindObjectsOfType<BossHealth>(true);
                if (allBossHealth.Length > 0)
                {
                    bossHealth = allBossHealth[0];
                }
            }

            var procedureBattle = procedureManager != null ? procedureManager.CurrentProcedure as ProcedureBattle : null;
            if (procedureBattle == null)
            {
                procedureBattle = FindObjectOfType<ProcedureBattle>();
            }

            procedureBattle?.SetBattleParticipants(formalPlayer, bossHealth);

            var battleHud = FindObjectOfType<BattleHudController>();
            if (battleHud != null && procedureManager != null)
            {
                battleHud.Configure(procedureManager, formalPlayer, bossHealth);
            }

            var bossBrain = bossHealth != null ? bossHealth.GetComponent<BossBrain>() : null;
            if (bossBrain == null)
            {
                var allBossBrains = Object.FindObjectsOfType<BossBrain>(true);
                if (allBossBrains.Length > 0)
                {
                    bossBrain = allBossBrains[0];
                }
            }

            bossBrain?.SetTargetPlayer(formalPlayer);

            if (VerboseLogging)
            {
                Debug.Log(
                    "RunScene formal player downstream references rebound. " +
                    "formalPlayer=" + formalPlayer.gameObject.name +
                    " procedureBattlePlayer=" + (formalPlayer != null ? formalPlayer.gameObject.name : "null") +
                    " battleHudBound=" + (battleHud != null) +
                    " bossBrainTargetBound=" + (bossBrain != null) +
                    " flowCameraTargetBound=" + (flowController != null && flowController.IsCameraFollowingFormalPlayer(formalPlayer)),
                    formalPlayer);
            }
        }

        private static void EnforceSingleRuntimeStateAttachment(PlayerHealth formalPlayer)
        {
            var stateOwner = formalPlayer != null ? formalPlayer.gameObject : null;
            var allStates = Object.FindObjectsOfType<RunCharacterRuntimeState>(true);
            for (var i = 0; i < allStates.Length; i++)
            {
                var state = allStates[i];
                if (state == null || state.gameObject == stateOwner)
                {
                    continue;
                }

                Debug.LogWarning(
                    "RunSceneSessionBootstrap removed RunCharacterRuntimeState from non-formal object: " +
                    state.gameObject.name,
                    state);
                Destroy(state);
            }
        }

        private static void EnforceSinglePlayerInstance(PlayerHealth primaryPlayer)
        {
            if (primaryPlayer == null)
            {
                return;
            }

            if (!primaryPlayer.gameObject.activeSelf)
            {
                primaryPlayer.gameObject.SetActive(true);
            }

            var allPlayers = Object.FindObjectsOfType<PlayerHealth>(true);
            for (var i = 0; i < allPlayers.Length; i++)
            {
                var candidate = allPlayers[i];
                if (candidate == null || candidate == primaryPlayer)
                {
                    continue;
                }

                if (string.Equals(candidate.gameObject.name, FormalPlayerName, System.StringComparison.Ordinal))
                {
                    candidate.gameObject.name = FormalPlayerName + "_Shadow";
                }

                candidate.gameObject.SetActive(false);
                Debug.LogWarning(
                    "RunSceneSessionBootstrap disabled extra PlayerHealth object to enforce single formal player chain: " +
                    candidate.gameObject.name,
                    candidate);
            }
        }

        private static void EnsurePlayerFirePointBinding(PlayerHealth primaryPlayer, WeaponController playerWeapon)
        {
            if (primaryPlayer == null || playerWeapon == null)
            {
                return;
            }

            var playerTransform = primaryPlayer.transform;
            var spawnPoint = playerWeapon.ProjectileSpawnPoint;
            var isValid =
                spawnPoint != null &&
                spawnPoint.gameObject.scene.IsValid() &&
                spawnPoint.IsChildOf(playerTransform);

            if (!isValid)
            {
                var fixedFirePoint = playerTransform.Find("PlayerFirePoint");
                if (fixedFirePoint == null)
                {
                    fixedFirePoint = new GameObject("PlayerFirePoint").transform;
                    fixedFirePoint.SetParent(playerTransform, false);
                }

                fixedFirePoint.localPosition = new Vector3(0.82f, 0f, 0f);
                fixedFirePoint.localRotation = Quaternion.identity;
                fixedFirePoint.localScale = Vector3.one;
                playerWeapon.SetProjectileSpawnPoint(fixedFirePoint);
                spawnPoint = fixedFirePoint;
            }

            if (VerboseLogging)
            {
                Debug.Log(
                    "RunScene formal player fire point bound. player=" + primaryPlayer.gameObject.name +
                    " firePoint=" + (spawnPoint != null ? spawnPoint.name : "null") +
                    " parent=" + (spawnPoint != null && spawnPoint.parent != null ? spawnPoint.parent.name : "null"),
                    primaryPlayer);
            }
        }

        private static void LogFormalPlayerEvidence(
            PlayerHealth formalPlayer,
            VerticalSliceFlowController flowController,
            PlayerPresentationSummary presentationSummary)
        {
            if (formalPlayer == null)
            {
                Debug.LogWarning(EvidenceLogPrefix + " formalPlayer=null");
                return;
            }

            if (!VerboseLogging)
            {
                return;
            }

            var formalObject = formalPlayer.gameObject;
            var hasController = formalObject.GetComponent<PlayerController>() != null;
            var hasHealth = formalObject.GetComponent<PlayerHealth>() != null;
            var hasWeapon = formalObject.GetComponent<WeaponController>() != null;
            var hasRuntimeState = formalObject.GetComponent<RunCharacterRuntimeState>() != null;
            var hasRenderer = formalObject.GetComponent<SpriteRenderer>() != null;

            var runtimeState = formalObject.GetComponent<RunCharacterRuntimeState>();
            var selected = RunSessionContext.SelectedCharacterData;

            var nameMatch = selected != null && runtimeState != null &&
                            string.Equals(runtimeState.CharacterName, selected.characterName, System.StringComparison.Ordinal);
            var redHealthMatch = selected != null && runtimeState != null && runtimeState.RedHealth == selected.redHealth;
            var blueArmorMatch = selected != null && runtimeState != null && runtimeState.BlueArmor == selected.blueArmor;
            var energyMatch = selected != null && runtimeState != null && runtimeState.Energy == selected.energy;
            var weapon1Match = selected != null && runtimeState != null &&
                               string.Equals(runtimeState.InitialWeapon1, selected.initialWeapon1, System.StringComparison.Ordinal);
            var weapon2Match = selected != null && runtimeState != null &&
                               string.Equals(runtimeState.InitialWeapon2, selected.initialWeapon2, System.StringComparison.Ordinal);

            var activePlayerCount = 0;
            var allPlayers = Object.FindObjectsOfType<PlayerHealth>(true);
            for (var i = 0; i < allPlayers.Length; i++)
            {
                var player = allPlayers[i];
                if (player != null && player.gameObject.activeInHierarchy)
                {
                    activePlayerCount++;
                }
            }

            var cameraFollowMatchesFormalPlayer = flowController != null && flowController.IsCameraFollowingFormalPlayer(formalPlayer);
            Debug.Log(
                EvidenceLogPrefix + " " +
                "formalPlayerName=" + formalObject.name +
                " activeInHierarchy=" + formalObject.activeInHierarchy +
                " components[PlayerController=" + hasController +
                ",PlayerHealth=" + hasHealth +
                ",WeaponController=" + hasWeapon +
                ",RunCharacterRuntimeState=" + hasRuntimeState +
                ",SpriteRenderer=" + hasRenderer +
                "] " +
                "runtimeMatch[characterName=" + nameMatch +
                ",redHealth=" + redHealthMatch +
                ",blueArmor=" + blueArmorMatch +
                ",energy=" + energyMatch +
                ",initialWeapon1=" + weapon1Match +
                ",initialWeapon2=" + weapon2Match +
                "] " +
                "runtimeValues[characterName=" + (runtimeState != null ? runtimeState.CharacterName : "null") +
                ",redHealth=" + (runtimeState != null ? runtimeState.RedHealth.ToString() : "null") +
                ",blueArmor=" + (runtimeState != null ? runtimeState.BlueArmor.ToString() : "null") +
                ",energy=" + (runtimeState != null ? runtimeState.Energy.ToString() : "null") +
                ",initialWeapon1=" + (runtimeState != null ? runtimeState.InitialWeapon1 : "null") +
                ",initialWeapon2=" + (runtimeState != null ? runtimeState.InitialWeapon2 : "null") +
                "] " +
                "selectedValues[characterName=" + (selected != null ? selected.characterName : "null") +
                ",redHealth=" + (selected != null ? selected.redHealth.ToString() : "null") +
                ",blueArmor=" + (selected != null ? selected.blueArmor.ToString() : "null") +
                ",energy=" + (selected != null ? selected.energy.ToString() : "null") +
                ",initialWeapon1=" + (selected != null ? selected.initialWeapon1 : "null") +
                ",initialWeapon2=" + (selected != null ? selected.initialWeapon2 : "null") +
                "] " +
                "visual[sprite=" + presentationSummary.SpriteName +
                ",source=" + presentationSummary.SpriteSource +
                ",tint=" + presentationSummary.Tint +
                "] " +
                "activePlayerHealthCount=" + activePlayerCount +
                " cameraFollowMatchesFormalPlayer=" + cameraFollowMatchesFormalPlayer,
                formalPlayer);
        }

        private void EnsurePlayerScale(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                return;
            }

            var fallbackScale = playerTransform.localScale;
            var targetScale = runtimePlayerBaseScale;

            if (Mathf.Abs(targetScale.x) < 0.01f)
            {
                targetScale.x = Mathf.Abs(fallbackScale.x) >= 0.01f ? fallbackScale.x : 1f;
            }

            if (Mathf.Abs(targetScale.y) < 0.01f)
            {
                targetScale.y = Mathf.Abs(fallbackScale.y) >= 0.01f ? fallbackScale.y : 1f;
            }

            if (Mathf.Abs(targetScale.z) < 0.01f)
            {
                targetScale.z = Mathf.Abs(fallbackScale.z) >= 0.01f ? fallbackScale.z : 1f;
            }

            if (!Mathf.Approximately(targetScale.x, fallbackScale.x) ||
                !Mathf.Approximately(targetScale.y, fallbackScale.y) ||
                !Mathf.Approximately(targetScale.z, fallbackScale.z))
            {
                playerTransform.localScale = targetScale;
            }
        }

        private static Font GetOverlayFont()
        {
            if (overlayFont != null)
            {
                return overlayFont;
            }

            overlayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (overlayFont == null)
            {
                overlayFont = Font.CreateDynamicFontFromOSFont("Arial", 18);
            }

            return overlayFont;
        }

        private static void CleanupLegacyRunUi()
        {
            DisableLegacyUiController<MenuPresetPanelController>();
            DisableLegacyUiController<RoleSelectionPanelController>();
            DisableLegacyUiObjectByName("MenuPresetPanel");
            DisableLegacyUiObjectByName("MenuPanel");
            DisableLegacyUiObjectByName("RoleSelectionPanel");
            DisableLegacyUiObjectByName("StartBattleButton");
            DisableLegacyUiObjectByName("CharacterSelectCanvas");
            DisableLegacyUiObjectByName("CharacterSelectRoot");
        }

        private static void DisableLegacyUiController<T>() where T : MonoBehaviour
        {
            var controllers = Object.FindObjectsOfType<T>(true);
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null || !controller.gameObject.scene.IsValid())
                {
                    continue;
                }

                controller.enabled = false;
                if (controller.gameObject.activeSelf)
                {
                    controller.gameObject.SetActive(false);
                }
            }
        }

        private static void DisableLegacyUiObjectByName(string objectName)
        {
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                var item = transforms[i];
                if (item == null ||
                    !item.gameObject.scene.IsValid() ||
                    !string.Equals(item.name, objectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (item.gameObject.activeSelf)
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        private static float ResolveWeaponValue(float configuredValue, string weaponName, WeaponStatType statType)
        {
            if (configuredValue > 0f)
            {
                return configuredValue;
            }

            var lowerName = string.IsNullOrWhiteSpace(weaponName) ? string.Empty : weaponName.ToLowerInvariant();
            var isShotgun = lowerName.Contains("shotgun") || lowerName.Contains("scatter");
            var isSmg = lowerName.Contains("smg") || lowerName.Contains("carbine") || lowerName.Contains("pulse");
            var isRail = lowerName.Contains("rail") || lowerName.Contains("sniper");

            switch (statType)
            {
                case WeaponStatType.Interval:
                    if (isShotgun)
                    {
                        return 0.36f;
                    }

                    if (isSmg)
                    {
                        return 0.12f;
                    }

                    if (isRail)
                    {
                        return 0.42f;
                    }

                    return 0.2f;
                case WeaponStatType.Speed:
                    if (isShotgun)
                    {
                        return 15f;
                    }

                    if (isSmg)
                    {
                        return 23f;
                    }

                    if (isRail)
                    {
                        return 28f;
                    }

                    return 20f;
                case WeaponStatType.Damage:
                    if (isShotgun)
                    {
                        return 22f;
                    }

                    if (isSmg)
                    {
                        return 9f;
                    }

                    if (isRail)
                    {
                        return 32f;
                    }

                    return 12f;
                case WeaponStatType.Lifetime:
                    if (isShotgun)
                    {
                        return 2.8f;
                    }

                    if (isSmg)
                    {
                        return 3.4f;
                    }

                    if (isRail)
                    {
                        return 3.9f;
                    }

                    return 3.2f;
                default:
                    return 3.2f;
            }
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static GameObject FindOrCreateUiChild(Transform parent, string name)
        {
            if (parent == null)
            {
                var orphan = new GameObject(name, typeof(RectTransform));
                return orphan;
            }

            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        private enum WeaponStatType
        {
            Interval = 0,
            Speed = 1,
            Damage = 2,
            Lifetime = 3,
        }
    }
}
