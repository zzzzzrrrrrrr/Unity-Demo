using UnityEngine;

namespace GameMain.GameLogic.Boss
{
    /// <summary>
    /// 2D boss movement driver.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float stopDistance = 0.6f;
        [Header("Simple Obstacle Bypass")]
        [SerializeField] [Min(0.01f)] private float blockedVelocityThreshold = 0.08f;
        [SerializeField] [Min(0.05f)] private float blockedDurationToBypass = 0.24f;
        [SerializeField] [Range(5f, 85f)] private float bypassTurnAngle = 52f;
        [SerializeField] [Min(0.05f)] private float bypassDuration = 0.32f;

        private Rigidbody2D cachedRigidbody;
        private Vector2 lastPosition;
        private bool hasLastPosition;
        private float blockedTimer;
        private float bypassEndTime;
        private float bypassSign = 1f;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedRigidbody.gravityScale = 0f;
            cachedRigidbody.freezeRotation = true;
            hasLastPosition = false;
            blockedTimer = 0f;
            bypassEndTime = 0f;
        }

        public void MoveTowards(Vector2 worldPosition, float speedScale)
        {
            var offset = worldPosition - (Vector2)transform.position;
            if (offset.sqrMagnitude <= stopDistance * stopDistance)
            {
                Stop();
                return;
            }

            var moveDirection = ApplyBypassDirection(offset.normalized);
            var velocity = moveDirection * moveSpeed * Mathf.Max(0f, speedScale);
            cachedRigidbody.velocity = velocity;
        }

        public void MoveAwayFrom(Vector2 worldPosition, float speedScale)
        {
            var offset = (Vector2)transform.position - worldPosition;
            if (offset.sqrMagnitude <= stopDistance * stopDistance)
            {
                offset = Random.insideUnitCircle.normalized;
            }

            var moveDirection = ApplyBypassDirection(offset.normalized);
            var velocity = moveDirection * moveSpeed * Mathf.Max(0f, speedScale);
            cachedRigidbody.velocity = velocity;
        }

        public void Stop()
        {
            cachedRigidbody.velocity = Vector2.zero;
            blockedTimer = 0f;
            bypassEndTime = 0f;
        }

        public void SetMoveSpeed(float value)
        {
            moveSpeed = Mathf.Max(0.1f, value);
        }

        private Vector2 ApplyBypassDirection(Vector2 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            var currentPosition = cachedRigidbody.position;
            if (!hasLastPosition)
            {
                hasLastPosition = true;
                lastPosition = currentPosition;
                return desiredDirection;
            }

            var movedDistance = (currentPosition - lastPosition).magnitude;
            lastPosition = currentPosition;

            var blockedThisFrame = cachedRigidbody.velocity.sqrMagnitude >= blockedVelocityThreshold * blockedVelocityThreshold
                && movedDistance <= blockedVelocityThreshold * Mathf.Max(0.01f, Time.deltaTime);
            if (blockedThisFrame)
            {
                blockedTimer += Time.deltaTime;
            }
            else
            {
                blockedTimer = Mathf.Max(0f, blockedTimer - Time.deltaTime * 0.5f);
            }

            if (blockedTimer >= blockedDurationToBypass && Time.time >= bypassEndTime)
            {
                blockedTimer = 0f;
                bypassSign = Random.value < 0.5f ? -1f : 1f;
                bypassEndTime = Time.time + Mathf.Max(0.05f, bypassDuration);
            }

            if (Time.time < bypassEndTime)
            {
                return (Quaternion.Euler(0f, 0f, bypassTurnAngle * bypassSign) * desiredDirection).normalized;
            }

            return desiredDirection;
        }
    }
}
