// Assets/Project/Features/Player/KRJakduZone.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// �۵� ���� ���� Ʈ���� ���Դϴ�.
    /// ��ҿ��� ��Ȱ��ȭ �����̸�, �۵� �ߵ� �� 1�����Ӹ� Ȱ��ȭ�� ���� �� ���� �����մϴ�.
    ///
    /// [���� ���]
    /// 1. Player ������ �� GameObject ���� �� �̸� "JakduZone"
    /// 2. Box Collider �߰� �� Is Trigger = true
    /// 3. �� ������Ʈ �߰�
    /// 4. Box Collider ũ��/��ġ�� �� �信�� ���� (�÷��̾� �������� ��ġ)
    /// 5. Layer �� ExecutionZone �Ǵ� Ignore Raycast
    /// 6. ���� �� ��Ȱ��ȭ ���·� �� �� (KRJakduSystem�� ����)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KRJakduZone : MonoBehaviour
    {
        private readonly HashSet<IDamageable> _hits = new HashSet<IDamageable>();

        private void OnEnable()
        {
            _hits.Clear();
        }

        // [2026-07-06 추가] 존이 평소 비활성 상태였다가 발동 시 켜지는 구조라,
        // 켜지는 순간 이미 겹쳐 있던 적은 OnTriggerStay가 아니라 OnTriggerEnter로 들어옵니다.
        // 기존에는 OnTriggerStay만 있어서 이 최초 접촉을 놓치고 있었습니다.
        private void OnTriggerEnter(Collider other)
        {
            RegisterHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            RegisterHit(other);
        }

        /// <summary>콜라이더에서 IDamageable을 찾아 수집 목록에 추가합니다. Enter/Stay가 동일 로직을 공유합니다.</summary>
        private void RegisterHit(Collider other)
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