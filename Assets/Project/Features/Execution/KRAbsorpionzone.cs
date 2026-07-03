// Assets/Project/Features/Player/KRAbsorptionZone.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 흡혼 판정 전용 트리거 존입니다.
    /// 플레이어 하위에 빈 GameObject를 만들고 Box Collider(Is Trigger)와
    /// 이 컴포넌트를 붙이세요.
    ///
    /// [설정 방법]
    /// 1. Player 하위에 빈 GameObject 생성 → 이름 "AbsorptionZone"
    /// 2. Box Collider 추가 → Is Trigger = true
    /// 3. 이 컴포넌트 추가
    /// 4. Box Collider 크기/위치를 씬 뷰에서 조절
    ///    (플레이어 정면으로 튀어나오게 배치하면 정면 판정)
    /// 5. Layer는 Ignore Raycast 권장 (무기 판정에 영향 없도록)
    ///
    /// [동작 방식]
    /// OnTriggerStay로 그로기 적을 후보 Set에 유지합니다.
    /// GetNearestTarget()은 후보 중 가장 가까운 대상을 반환합니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KRAbsorptionZone : MonoBehaviour
    {
        private readonly HashSet<IDamageable> _candidates = new HashSet<IDamageable>();

        private void OnTriggerStay(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead || !damageable.IsGroggy) return;

            _candidates.Add(damageable);
        }

        private void OnTriggerExit(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
                _candidates.Remove(damageable);
        }

        private void LateUpdate()
        {
            // 죽었거나 그로기가 풀린 후보를 매 프레임 정리합니다.
            _candidates.RemoveWhere(c => c == null || c.IsDead || !c.IsGroggy);
        }

        /// <summary>현재 존 안에 처형 가능한 대상이 있는지 여부.</summary>
        public bool HasTarget => _candidates.Count > 0;

        /// <summary>
        /// 존 안의 그로기 적 중 가장 가까운 대상을 반환합니다.
        /// 없으면 null을 반환합니다.
        /// </summary>
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