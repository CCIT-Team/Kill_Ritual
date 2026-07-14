// Assets/Project/Scripts/01_Core/Damage/KRDamageContext.cs
using UnityEngine;

namespace KillRitual.Core.Damage
{
    public enum KRDamageType
    {
        Fire = 0,

        Water = 1,

        Wood = 2,

        Earth = 3,

        Metal = 4
    }

    public readonly struct KRDamageContext
    {
        public readonly float DamageAmount;

        public readonly KRDamageType Type;

        public readonly Vector3 HitPoint;

        public readonly Vector3 Direction;

        public readonly bool IsMuryeongReflected;

        public KRDamageContext(float damageAmount, KRDamageType type, Vector3 hitPoint, Vector3 direction, bool isMuryeongReflected = false)
        {
            DamageAmount = damageAmount;
            Type = type;
            HitPoint = hitPoint;
            Direction = direction;
            IsMuryeongReflected = isMuryeongReflected;
        }
    }
}