// Assets/Project/Features/Player/KRJakduZone.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Player.Combat
{
    [RequireComponent(typeof(Collider))]
    public sealed class KRJakduZone : MonoBehaviour
    {
        private readonly HashSet<IDamageable> _hits = new HashSet<IDamageable>();

        private void OnEnable()
        {
            _hits.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            RegisterHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            RegisterHit(other);
        }

        private void RegisterHit(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;
            // 플레이어 자신 제외
            if (damageable is KRCombatSystem) return;

            _hits.Add(damageable);
        }

        public IReadOnlyCollection<IDamageable> GetHits() => _hits;
    }
}