using UnityEngine;

namespace ARPGDemo
{
    public struct DamageInfo
    {
        public GameObject Source;
        public GameObject Target;
        public float Amount;
        public Vector3 HitPoint;
        public Vector3 HitDirection;
        public int ComboIndex;
    }
}
