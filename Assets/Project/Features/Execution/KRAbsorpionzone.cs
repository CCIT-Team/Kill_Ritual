// Assets/Project/Features/Player/KRAbsorptionZone.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Player.Combat
{
    [RequireComponent(typeof(Collider))]
    public sealed class KRAbsorptionZone : MonoBehaviour
    {
        private readonly HashSet<IDamageable> _candidates = new HashSet<IDamageable>();

        private void OnTriggerStay(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead || !damageable.IsGroggy) return;

            if (_candidates.Add(damageable))
            {
                var enemyBase = other.GetComponentInParent<KillRitual.Enemies.KREnemyBase>();
                enemyBase?.GroggyOutline?.SetInRange(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            if (_candidates.Remove(damageable))
            {
                // 처형 가능 범위에서 벗어난 순간 범위 이탈을 알립니다.
                var enemyBase = other.GetComponentInParent<KillRitual.Enemies.KREnemyBase>();
                enemyBase?.GroggyOutline?.SetInRange(false);
            }
        }

        private void LateUpdate()
        {
            // 죽었거나 그로기가 풀린 후보를 정리하고 테두리도 끕니다.
            _candidates.RemoveWhere(c =>
            {
                if (c == null || c.IsDead || !c.IsGroggy)
                {
                    if (c is KillRitual.Enemies.KREnemyBase enemy)
                        enemy.GroggyOutline?.SetInRange(false);
                    return true;
                }
                return false;
            });
        }

        public bool HasTarget => _candidates.Count > 0;

        public IDamageable GetNearestTarget()
        {
            IDamageable best = null;
            float bestDistance = float.MaxValue;

            foreach (IDamageable candidate in _candidates)
            {
                if (candidate == null || candidate.IsDead || !candidate.IsGroggy) continue;

                float distance = Vector3.Distance(transform.position, candidate.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}