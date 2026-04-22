// Path: Assets/_Scripts/Core/PlayerCoreData.cs
using System.Collections;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Core
{
    /// <summary>
    /// </summary>
    [RequireComponent(typeof(ActorStats))]
    public class PlayerCoreData : MonoBehaviour
    {
        [Header("Revive Settings")]
        [SerializeField] private bool enableAutoRevive = true;
        [SerializeField] private int initialReviveCount = 1;
        [SerializeField] private float reviveDelay = 1.2f;
        [SerializeField] private float reviveHealthPercent = 0.4f;
        [SerializeField] private float reviveManaPercent = 0.3f;
        [SerializeField] private float postReviveInvincibleTime = 1.2f;

        private ActorStats actorStats;
        private Coroutine reviveRoutine;
        private int remainingReviveCount;

        public int RemainingReviveCount => remainingReviveCount;
        public bool IsReviving => reviveRoutine != null;

        private void Awake()
        {
            actorStats = GetComponent<ActorStats>();
            remainingReviveCount = Mathf.Max(0, initialReviveCount);
        }

        /// <summary>
        /// </summary>
        public bool TryConsumeReviveAndStart()
        {
            if (!enableAutoRevive)
            {
                return false;
            }

            if (actorStats == null || !actorStats.IsDead)
            {
                return false;
            }

            if (remainingReviveCount <= 0)
            {
                return false;
            }

            if (reviveRoutine != null)
            {
                return true;
            }

            remainingReviveCount--;

            EventCenter.Broadcast(new PlayerReviveEvent(actorStats.ActorId, remainingReviveCount, true));

            reviveRoutine = StartCoroutine(ReviveRoutine());
            return true;
        }

        /// <summary>
        /// </summary>
        public void SetRemainingReviveCount(int count)
        {
            remainingReviveCount = Mathf.Max(0, count);
        }

        /// <summary>
        /// </summary>
        public void ResetReviveCount()
        {
            remainingReviveCount = Mathf.Max(0, initialReviveCount);
        }

        private IEnumerator ReviveRoutine()
        {
            float safeDelay = Mathf.Max(0f, reviveDelay);

            yield return new WaitForSecondsRealtime(safeDelay);

            actorStats.Revive(reviveHealthPercent, reviveManaPercent);
            actorStats.AddTemporaryInvincible(postReviveInvincibleTime);

            EventCenter.Broadcast(new PlayerReviveEvent(actorStats.ActorId, remainingReviveCount, false));

            reviveRoutine = null;
        }
    }
}

