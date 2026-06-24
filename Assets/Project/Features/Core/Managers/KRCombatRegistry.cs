// Assets/Project/Scripts/01_Core/Managers/KRCombatRegistry.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Core.Managers
{
    /// <summary>
    /// [최적화] Collider → IDamageable 사전 매핑 캐시입니다.
    ///
    /// 문제 배경:
    ///   광역 폭발 판정(Explode)은 OverlapSphereNonAlloc으로 콜라이더 목록을 수집한 뒤,
    ///   각 콜라이더마다 GetComponentInParent<IDamageable>()를 호출해 피격 주체를 식별했습니다.
    ///   이 런타임 계층 탐색은 콜라이더 수에 비례해 누적되며, 중복 제거를 위한 이중 루프(O(n²))와
    ///   결합되어 수십 명 밀집 전투에서 프레임 드랍의 원인이 됩니다.
    ///
    /// 해결 원리 (Sweep and Prune의 내로우페이즈 최적화 원칙 적용):
    ///   적이 스폰될 때 자신의 모든 콜라이더를 이 딕셔너리에 사전 등록합니다.
    ///   폭발 판정 시에는 GetComponentInParent 대신 딕셔너리를 O(1) 해시 조회로 대체합니다.
    ///   런타임 탐색이 완전히 제거되어 내로우페이즈 비용이 상수 시간으로 고정됩니다.
    ///
    /// 사용 패턴:
    ///   KREnemyEntity.OnEnable()  → KRManagers.Combat.Register(collider, this)
    ///   KREnemyEntity.OnDisable() → KRManagers.Combat.Unregister(collider)
    ///   KRPhysicsProjectile       → KRManagers.Combat.Lookup(collider)
    /// </summary>
    public sealed class KRCombatRegistry
    {
        // 콜라이더 → 피격 가능 주체 매핑. 해시 기반이므로 조회가 O(1)입니다.
        private readonly Dictionary<Collider, IDamageable> _map = new Dictionary<Collider, IDamageable>();

        /// <summary>
        /// 피격 가능 개체의 콜라이더를 캐시에 등록합니다.
        /// 하나의 개체가 여러 부위별 콜라이더를 가질 경우, 모두 같은 IDamageable로 등록합니다.
        /// </summary>
        public void Register(Collider col, IDamageable damageable)
        {
            if (col == null || damageable == null) return;
            _map[col] = damageable;
        }

        /// <summary>개체가 씬에서 제거될 때 해당 콜라이더 항목을 캐시에서 삭제합니다.</summary>
        public void Unregister(Collider col)
        {
            if (col != null) _map.Remove(col);
        }

        /// <summary>
        /// 콜라이더로 피격 가능 주체를 O(1)로 조회합니다.
        /// 등록되지 않은 콜라이더(벽, 환경 오브젝트 등)는 null을 반환합니다.
        /// </summary>
        public IDamageable Lookup(Collider col)
        {
            if (col == null) return null;
            _map.TryGetValue(col, out IDamageable result);
            return result;
        }

        /// <summary>씬 전환 등 전체 초기화가 필요할 때 사용합니다.</summary>
        public void Clear() => _map.Clear();

        /// <summary>현재 등록된 콜라이더 수. 디버그/테스트용.</summary>
        public int Count => _map.Count;
    }
}
