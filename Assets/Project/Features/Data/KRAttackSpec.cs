using System;
using UnityEngine;

namespace KillRitual.Weapons
{
    [Serializable]
    public sealed class KRAttackSpec
    {
        [Header("기본 정보")]
        public string attackName = "Attack";
        public KRAttackInputType inputType = KRAttackInputType.Tap;

        [Header("자원 / 피해")]
        public float resourceCost = 5f;
        public float damage = 10f;
        public float range = 50f;

        [Header("Tap / 공통 쿨타임")]
        public float cooldown = 0.25f;

        [Header("HoldAuto")]
        public float fireInterval = 0.12f;
        public float minFireInterval = 0.06f;
        public float rampUpTime = 1.5f;

        [Header("ChargeRelease")]
        public float minChargeTime = 0.15f;
        public float maxChargeTime = 1.2f;
        public float minChargeDamageMultiplier = 0.5f;
        public float maxChargeDamageMultiplier = 2.0f;
    }
}
