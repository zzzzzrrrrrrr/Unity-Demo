using UnityEngine;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class StarterARPGActionStateMachine : MonoBehaviour
    {
        [SerializeField] private StarterARPGActionState currentState = StarterARPGActionState.Idle;

        public StarterARPGActionState CurrentState => currentState;

        public bool IsBusy =>
            currentState == StarterARPGActionState.Attack ||
            currentState == StarterARPGActionState.Dodge ||
            currentState == StarterARPGActionState.Skill ||
            currentState == StarterARPGActionState.Hurt ||
            currentState == StarterARPGActionState.Dead;

        public bool CanAttack => currentState == StarterARPGActionState.Idle || currentState == StarterARPGActionState.Move;
        public bool CanDodge => currentState == StarterARPGActionState.Idle || currentState == StarterARPGActionState.Move;
        public bool CanCastSkill => currentState == StarterARPGActionState.Idle || currentState == StarterARPGActionState.Move;
        public bool CanMoveNormally => currentState == StarterARPGActionState.Idle || currentState == StarterARPGActionState.Move;

        public bool TryEnterAttack()
        {
            if (!CanAttack)
            {
                return false;
            }

            currentState = StarterARPGActionState.Attack;
            return true;
        }

        public bool TryEnterDodge()
        {
            if (!CanDodge)
            {
                return false;
            }

            currentState = StarterARPGActionState.Dodge;
            return true;
        }

        public bool TryEnterSkill()
        {
            if (!CanCastSkill)
            {
                return false;
            }

            currentState = StarterARPGActionState.Skill;
            return true;
        }

        public void SetHurt()
        {
            if (currentState == StarterARPGActionState.Dead)
            {
                return;
            }

            currentState = StarterARPGActionState.Hurt;
        }

        public void SetDead()
        {
            currentState = StarterARPGActionState.Dead;
        }

        public bool SetLocomotion(bool isMoving)
        {
            if (!CanMoveNormally)
            {
                return false;
            }

            currentState = isMoving ? StarterARPGActionState.Move : StarterARPGActionState.Idle;
            return true;
        }

        public bool ReturnToLocomotion(bool isMoving)
        {
            if (currentState == StarterARPGActionState.Dead)
            {
                return false;
            }

            currentState = isMoving ? StarterARPGActionState.Move : StarterARPGActionState.Idle;
            return true;
        }
    }
}
