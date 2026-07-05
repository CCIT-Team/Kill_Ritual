// Assets/Project/Features/Player/KRJakduZone.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 작두 판정 전용 트리거 존입니다.
    /// 평소에는 비활성화 상태이며, 작두 발동 시 1프레임만 활성화해 범위 내 적을 수집합니다.
    ///
    /// [설정 방법]
    /// 1. Player 하위에 빈 GameObject 생성 → 이름 "JakduZone"
    /// 2. Box Collider 추가 → Is Trigger = true
    /// 3. 이 컴포넌트 추가
    /// 4. Box Collider 크기/위치를 씬 뷰에서 조절 (플레이어 정면으로 배치)
    /// 5. Layer → ExecutionZone 또는 Ignore Raycast
    /// 6. 시작 시 비활성화 상태로 둘 것 (KRJakduSystem이 제어)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KRJakduZone : MonoBehaviour
    {
        private readonly HashSet<IDamageable> _hits = new HashSet<IDamageable>();

        private void OnEnable()
        {
            _hits.Clear();
        }

        private void OnTriggerStay(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;
            // 플레이어 자신 제외
            if (damageable is KRCombatSystem) return;

            _hits.Add(damageable);
        }

        /// <summary>수집된 피격 대상 목록을 반환합니다.</summary>
        public IReadOnlyCollection<IDamageable> GetHits() => _hits;
    }
}