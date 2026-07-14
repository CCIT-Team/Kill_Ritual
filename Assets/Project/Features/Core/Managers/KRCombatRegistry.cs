// Assets/Project/Scripts/01_Core/Managers/KRCombatRegistry.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Core.Managers
{
    public sealed class KRCombatRegistry
    {
        // 콜라이더 → 피격 가능 주체 매핑. 해시 기반이므로 조회가 O(1)입니다.
        private readonly Dictionary<Collider, IDamageable> _map = new Dictionary<Collider, IDamageable>();

        public void Register(Collider col, IDamageable damageable)
        {
            if (col == null || damageable == null) return;
            _map[col] = damageable;
        }

        public void Unregister(Collider col)
        {
            if (col != null) _map.Remove(col);
        }

        public IDamageable Lookup(Collider col)
        {
            if (col == null) return null;
            _map.TryGetValue(col, out IDamageable result);
            return result;
        }

        public void Clear() => _map.Clear();

        public int Count => _map.Count;
    }
}
