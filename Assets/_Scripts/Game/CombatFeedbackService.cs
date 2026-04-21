// 路径: Assets/_Scripts/Game/CombatFeedbackService.cs
// 功能: 轻量战斗手感层，提供受击停顿与镜头震动。
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Game
{
    [DisallowMultipleComponent]
    public class CombatFeedbackService : MonoBehaviour
    {
        [Header("Hit Stop")]
        [SerializeField] private bool enableHitStop = true;
        [SerializeField] private float hitStopDuration = 0.035f;
        [SerializeField] [Range(0.01f, 1f)] private float hitStopTimeScale = 0.08f;
        [SerializeField] private float criticalHitStopDuration = 0.06f;
        [SerializeField] [Range(0.01f, 1f)] private float criticalHitStopTimeScale = 0.05f;
        [SerializeField] private float minHitStopInterval = 0.03f;

        [Header("Camera Shake")]
        [SerializeField] private bool enableCameraShake = true;
        [SerializeField] private float shakeDuration = 0.08f;
        [SerializeField] private float shakeAmplitude = 0.09f;
        [SerializeField] private float shakeFrequency = 24f;
        [SerializeField] private float criticalShakeDuration = 0.14f;
        [SerializeField] private float criticalShakeAmplitude = 0.16f;
        [SerializeField] private float criticalShakeFrequency = 28f;
        [SerializeField] private float minShakeInterval = 0.02f;

        [Header("Debug")]
        [SerializeField] private bool verboseLog;

        private Coroutine hitStopRoutine;
        private Coroutine shakeRoutine;
        private Transform cameraTarget;
        private Vector3 cameraBasePosition;
        private bool isFlowPaused;
        private float lastHitStopTriggerTime = -99f;
        private float lastShakeTriggerTime = -99f;

        private void OnEnable()
        {
            EventCenter.AddListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.AddListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
            EventCenter.AddListener<HitStopEvent>(OnHitStopEvent);
            EventCenter.AddListener<CameraShakeEvent>(OnCameraShakeEvent);
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.RemoveListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
            EventCenter.RemoveListener<HitStopEvent>(OnHitStopEvent);
            EventCenter.RemoveListener<CameraShakeEvent>(OnCameraShakeEvent);
            ResetCameraPosition();
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.FinalDamage <= 0)
            {
                return;
            }

            if (enableHitStop)
            {
                float duration = evt.IsCritical ? criticalHitStopDuration : hitStopDuration;
                float scale = evt.IsCritical ? criticalHitStopTimeScale : hitStopTimeScale;
                TriggerHitStop(duration, scale);
            }

            if (enableCameraShake)
            {
                float duration = evt.IsCritical ? criticalShakeDuration : shakeDuration;
                float amplitude = evt.IsCritical ? criticalShakeAmplitude : shakeAmplitude;
                float frequency = evt.IsCritical ? criticalShakeFrequency : shakeFrequency;
                TriggerCameraShake(duration, amplitude, frequency);
            }
        }

        private void OnGameFlowChanged(GameFlowStateChangedEvent evt)
        {
            isFlowPaused = evt.CurrentState != GameFlowState.Playing;
        }

        private void OnHitStopEvent(HitStopEvent evt)
        {
            TriggerHitStop(evt.Duration, evt.TimeScale);
        }

        private void OnCameraShakeEvent(CameraShakeEvent evt)
        {
            TriggerCameraShake(evt.Duration, evt.Amplitude, evt.Frequency);
        }

        private void TriggerHitStop(float duration, float timeScale)
        {
            if (!enableHitStop || isFlowPaused)
            {
                return;
            }

            if (Time.unscaledTime - lastHitStopTriggerTime < Mathf.Max(0f, minHitStopInterval))
            {
                return;
            }

            if (duration <= 0f)
            {
                return;
            }

            lastHitStopTriggerTime = Time.unscaledTime;

            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
            }

            hitStopRoutine = StartCoroutine(CoHitStop(Mathf.Max(0.01f, duration), Mathf.Clamp(timeScale, 0.01f, 1f)));
        }

        private System.Collections.IEnumerator CoHitStop(float duration, float timeScale)
        {
            float restoreScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = timeScale;

            if (verboseLog)
            {
                Debug.Log($"[CombatFeedback] HitStop duration={duration:F3}, scale={timeScale:F2}", this);
            }

            yield return new WaitForSecondsRealtime(duration);

            if (!isFlowPaused)
            {
                Time.timeScale = restoreScale;
            }

            hitStopRoutine = null;
        }

        private void TriggerCameraShake(float duration, float amplitude, float frequency)
        {
            if (!enableCameraShake)
            {
                return;
            }

            if (Time.unscaledTime - lastShakeTriggerTime < Mathf.Max(0f, minShakeInterval))
            {
                return;
            }

            if (duration <= 0f || amplitude <= 0f || frequency <= 0f)
            {
                return;
            }

            lastShakeTriggerTime = Time.unscaledTime;
            if (!TryResolveCameraTarget())
            {
                return;
            }

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                ResetCameraPosition();
            }

            shakeRoutine = StartCoroutine(CoCameraShake(duration, amplitude, frequency));
        }

        private System.Collections.IEnumerator CoCameraShake(float duration, float amplitude, float frequency)
        {
            if (cameraTarget == null)
            {
                shakeRoutine = null;
                yield break;
            }

            cameraBasePosition = cameraTarget.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float time = Time.unscaledTime * frequency;
                float x = (Mathf.PerlinNoise(time, 0.13f) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(0.47f, time) - 0.5f) * 2f;
                cameraTarget.localPosition = cameraBasePosition + new Vector3(x, y, 0f) * amplitude;
                yield return null;
            }

            ResetCameraPosition();
            shakeRoutine = null;
        }

        private bool TryResolveCameraTarget()
        {
            if (cameraTarget != null)
            {
                return true;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return false;
            }

            cameraTarget = cam.transform;
            cameraBasePosition = cameraTarget.localPosition;
            return true;
        }

        private void ResetCameraPosition()
        {
            if (cameraTarget != null)
            {
                cameraTarget.localPosition = cameraBasePosition;
            }
        }
    }
}
