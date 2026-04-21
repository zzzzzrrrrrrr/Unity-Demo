// Path: Assets/_Scripts/Core/GameEventDefs.cs
using UnityEngine;

namespace ARPGDemo.Core
{
    /// <summary>
    /// </summary>
    public enum GameFlowState
    {
        MainMenu = 0,
        Playing = 1,
        Paused = 2,
        Victory = 3,
        Defeat = 4
    }

    /// <summary>
    /// </summary>
    public enum ActorStateType
    {
        None = 0,
        Idle = 1,
        Move = 2,
        Jump = 3,
        Patrol = 4,
        Chase = 5,
        Attack = 6,
        Hurt = 7,
        Death = 8
    }

    /// <summary>
    /// </summary>
    public enum UIPanelType
    {
        MainMenu = 0,
        Pause = 1,
        Result = 2,
        Settings = 3
    }

    /// <summary>
    /// </summary>
    public enum ComboStageType
    {
        None = -1,
        Light1 = 0,
        Light2 = 1,
        Heavy = 2
    }

    public readonly struct ActorHealthChangedEvent
    {
        public readonly string ActorId;
        public readonly ActorTeam Team;
        public readonly float CurrentHp;
        public readonly float MaxHp;
        public readonly float CurrentMp;
        public readonly float MaxMp;
        public readonly bool IsDead;
        public readonly Vector3 WorldPosition;

        public ActorHealthChangedEvent(
            string actorId,
            ActorTeam team,
            float currentHp,
            float maxHp,
            float currentMp,
            float maxMp,
            bool isDead,
            Vector3 worldPosition)
        {
            ActorId = actorId;
            Team = team;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            CurrentMp = currentMp;
            MaxMp = maxMp;
            IsDead = isDead;
            WorldPosition = worldPosition;
        }
    }

    public readonly struct ActorStateChangedEvent
    {
        public readonly string ActorId;
        public readonly ActorTeam Team;
        public readonly ActorStateType State;

        public ActorStateChangedEvent(string actorId, ActorTeam team, ActorStateType state)
        {
            ActorId = actorId;
            Team = team;
            State = state;
        }
    }

    public readonly struct DamageAppliedEvent
    {
        public readonly string AttackerId;
        public readonly string TargetId;
        public readonly int FinalDamage;
        public readonly bool IsCritical;
        public readonly Vector3 SourcePosition;
        public readonly Vector3 HitPosition;

        public DamageAppliedEvent(
            string attackerId,
            string targetId,
            int finalDamage,
            bool isCritical,
            Vector3 sourcePosition,
            Vector3 hitPosition)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            FinalDamage = finalDamage;
            IsCritical = isCritical;
            SourcePosition = sourcePosition;
            HitPosition = hitPosition;
        }
    }

    public readonly struct ActorDiedEvent
    {
        public readonly string ActorId;
        public readonly ActorTeam Team;
        public readonly Vector3 WorldPosition;

        public ActorDiedEvent(string actorId, ActorTeam team, Vector3 worldPosition)
        {
            ActorId = actorId;
            Team = team;
            WorldPosition = worldPosition;
        }
    }

    public readonly struct ActorRevivedEvent
    {
        public readonly string ActorId;
        public readonly ActorTeam Team;
        public readonly Vector3 WorldPosition;

        public ActorRevivedEvent(string actorId, ActorTeam team, Vector3 worldPosition)
        {
            ActorId = actorId;
            Team = team;
            WorldPosition = worldPosition;
        }
    }

    public readonly struct PlayerReviveEvent
    {
        public readonly string PlayerId;
        public readonly int RemainingReviveCount;
        public readonly bool IsReviving;

        public PlayerReviveEvent(string playerId, int remainingReviveCount, bool isReviving)
        {
            PlayerId = playerId;
            RemainingReviveCount = remainingReviveCount;
            IsReviving = isReviving;
        }
    }

    public readonly struct ComboStageChangedEvent
    {
        public readonly string ActorId;
        public readonly ComboStageType StageType;
        public readonly bool IsStarting;

        public ComboStageChangedEvent(string actorId, ComboStageType stageType, bool isStarting)
        {
            ActorId = actorId;
            StageType = stageType;
            IsStarting = isStarting;
        }
    }

    public readonly struct GameFlowStateChangedEvent
    {
        public readonly GameFlowState PreviousState;
        public readonly GameFlowState CurrentState;
        public readonly string Message;

        public GameFlowStateChangedEvent(GameFlowState previousState, GameFlowState currentState, string message)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            Message = message;
        }
    }

    public readonly struct UIRequestEvent
    {
        public readonly UIPanelType PanelType;
        public readonly bool IsOpen;
        public readonly string Message;

        public UIRequestEvent(UIPanelType panelType, bool isOpen, string message)
        {
            PanelType = panelType;
            IsOpen = isOpen;
            Message = message;
        }
    }

    public readonly struct SaveLoadEvent
    {
        public readonly bool IsSave;
        public readonly bool IsSuccess;

        public SaveLoadEvent(bool isSave, bool isSuccess)
        {
            IsSave = isSave;
            IsSuccess = isSuccess;
        }
    }

    public readonly struct HitStopEvent
    {
        public readonly float Duration;
        public readonly float TimeScale;

        public HitStopEvent(float duration, float timeScale)
        {
            Duration = duration;
            TimeScale = timeScale;
        }
    }

    public readonly struct CameraShakeEvent
    {
        public readonly float Duration;
        public readonly float Amplitude;
        public readonly float Frequency;

        public CameraShakeEvent(float duration, float amplitude, float frequency)
        {
            Duration = duration;
            Amplitude = amplitude;
            Frequency = frequency;
        }
    }

    public readonly struct DamagePopupEvent
    {
        public readonly Vector3 WorldPosition;
        public readonly int Damage;
        public readonly bool IsCritical;
        public readonly string TargetId;

        public DamagePopupEvent(Vector3 worldPosition, int damage, bool isCritical, string targetId)
        {
            WorldPosition = worldPosition;
            Damage = damage;
            IsCritical = isCritical;
            TargetId = targetId;
        }
    }

    public readonly struct AudioPlayEvent
    {
        public readonly AudioClip Clip;
        public readonly Vector3 WorldPosition;
        public readonly float VolumeScale;
        public readonly bool IsUISound;

        public AudioPlayEvent(AudioClip clip, Vector3 worldPosition, float volumeScale, bool isUISound)
        {
            Clip = clip;
            WorldPosition = worldPosition;
            VolumeScale = volumeScale;
            IsUISound = isUISound;
        }
    }

    public readonly struct SettingsChangedEvent
    {
        public readonly float MasterVolume;
        public readonly float SfxVolume;
        public readonly float BgmVolume;
        public readonly float MouseSensitivity;

        public SettingsChangedEvent(float masterVolume, float sfxVolume, float bgmVolume, float mouseSensitivity)
        {
            MasterVolume = masterVolume;
            SfxVolume = sfxVolume;
            BgmVolume = bgmVolume;
            MouseSensitivity = mouseSensitivity;
        }
    }

    public readonly struct ChestOpenedEvent
    {
        public readonly string ItemId;
        public readonly string ItemDisplayName;
        public readonly int Amount;
        public readonly Vector3 WorldPosition;

        public ChestOpenedEvent(string itemId, string itemDisplayName, int amount, Vector3 worldPosition)
        {
            ItemId = itemId;
            ItemDisplayName = itemDisplayName;
            Amount = amount;
            WorldPosition = worldPosition;
        }
    }
}

