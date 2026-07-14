// Assets/Project/Features/Enemies/MakeNew/KRBossArmorShard.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class KRBossArmorShard : MonoBehaviour
    {
        private Rigidbody _rigidbody;
        private SphereCollider _sphereCollider;

        private Vector3 _velocity;
        private float _damage;
        private bool _willExplode;
        private float _explodeDelay;
        private float _explosionRadius;
        private LayerMask _hitLayerMask;
        private LayerMask _damageableLayerMask;
        private IDamageable _owner;

        private bool _stuck;

        [Header("폭발 예고 (2026-07-08 신규)")]
        [Tooltip("'맞지 않아도 죽는다' 버그 원인 — 2페이즈에서는 철갑이 바닥/벽에 꽂힌 뒤 " +
                 "explodeDelay초 후 폭발했는데, 그동안 아무 시각 신호가 없어서 플레이어는 " +
                 "투사체가 빗나간 줄 알고 그 자리에 서 있다가 아무 예고 없이 폭발 피해를 받았습니다. " +
                 "이제 꽂히는 즉시 폭발 반경만큼 바닥에 원을 그리고, 터지기 직전까지 점점 빠르게 " +
                 "깜빡이며 커져서 도망칠 시간을 눈으로 알 수 있게 합니다.")]
        [SerializeField] private Color _explosionRingColor = new Color(1f, 0.2f, 0f, 1f);
        [Min(3)] [SerializeField] private int _explosionRingSegments = 32;

        private LineRenderer _explosionRing;
        private Transform _visualTransform;
        private Vector3 _visualBaseScale = Vector3.one;

        public void Launch(Vector3 velocity, float damage, LayerMask hitLayerMask, LayerMask damageableLayerMask,
            IDamageable owner, bool willExplode = false, float explodeDelay = 1.5f, float explosionRadius = 2.5f)
        {
            _velocity = velocity;
            _damage = damage;
            _hitLayerMask = hitLayerMask;
            _damageableLayerMask = damageableLayerMask;
            _owner = owner;
            _willExplode = willExplode;
            _explodeDelay = explodeDelay;
            _explosionRadius = explosionRadius;

            if (velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
            _sphereCollider = col as SphereCollider;

            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                _visualTransform = visual;
                _visualBaseScale = visual.localScale;
            }
        }

        private void FixedUpdate()
        {
            if (_stuck) return;
            if (_velocity.sqrMagnitude <= 0f) return;

            float moveDistance = _velocity.magnitude * Time.fixedDeltaTime;
            Vector3 direction = _velocity.normalized;
            float castRadius = _sphereCollider != null ? _sphereCollider.radius : 0.08f;

            if (Physics.SphereCast(_rigidbody.position, castRadius, direction, out RaycastHit hit, moveDistance,
                    _hitLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsIgnoredCollider(hit.collider))
                {
                    ResolveHit(hit.collider, hit.point);
                    if (_stuck) return;
                }
            }

            _rigidbody.MovePosition(_rigidbody.position + _velocity * Time.fixedDeltaTime);
        }

        private bool IsIgnoredCollider(Collider other)
        {
            if (other.GetComponentInParent<KRBossArmorShard>() != null) return true;

            if (IsOwnerHierarchy(other)) return true;

            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_stuck) return;

            if (IsIgnoredCollider(other)) return;

            if (((1 << other.gameObject.layer) & _hitLayerMask.value) == 0) return;

            if (other.isTrigger) return;

            Vector3 point = other.ClosestPoint(transform.position);
            ResolveHit(other, point);
        }

        private void ResolveHit(Collider other, Vector3 point)
        {
            IDamageable target = other.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();
            if (target == null) target = other.GetComponentInParent<IDamageable>();

            if (target != null && !target.IsDead)
            {
                Debug.Log($"[철갑 조각] 직접 명중 — 철갑 위치 {transform.position}, " +
                          $"충돌지점 {point}, 대상 콜라이더({other.name}) 중심 {other.transform.position}, " +
                          $"철갑-대상중심 거리 {Vector3.Distance(transform.position, other.transform.position):F2}m, " +
                          $"콜라이더 반지름 {(other as SphereCollider)?.radius}");

                // 플레이어(또는 다른 피격 대상)를 직접 맞췄으면 즉시 피해를 주고 사라집니다.
                var context = new KRDamageContext(_damage, KRDamageType.Metal, point, _velocity.normalized);
                target.TakeDamage(context);
                _stuck = true; // FixedUpdate 쪽에서 이번 스텝 MovePosition을 건너뛰게 함.
                Destroy(gameObject);
                return;
            }

            if (target != null) return; // 이미 죽은 대상 등 — 무시하고 계속 날아감.

            // 바닥/벽 등 피격 대상이 아닌 것에 맞았으면 그 자리에 박혀서 남습니다.
            _stuck = true;
            _velocity = Vector3.zero;
            transform.position = point;

            if (_willExplode)
                StartCoroutine(ExplodeCountdown());
            else
                Destroy(gameObject, 3f); // 안 터지는 버전(1페이즈)은 잠시 후 조용히 정리
        }

        private IEnumerator ExplodeCountdown()
        {
            CreateExplosionRing();

            float elapsed = 0f;
            while (elapsed < _explodeDelay)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _explodeDelay);

                // 폭발이 가까워질수록 깜빡이는 속도와 커지는 정도를 둘 다 키워서 긴박감을 줍니다.
                float pulseSpeed = Mathf.Lerp(2f, 14f, progress);
                float pulse = (Mathf.Sin(elapsed * pulseSpeed) + 1f) * 0.5f;

                if (_explosionRing != null)
                {
                    Color c = _explosionRingColor;
                    c.a = Mathf.Lerp(0.25f, 1f, pulse);
                    _explosionRing.startColor = c;
                    _explosionRing.endColor = c;
                }

                if (_visualTransform != null)
                {
                    float scale = 1f + pulse * Mathf.Lerp(0.15f, 0.6f, progress);
                    _visualTransform.localScale = _visualBaseScale * scale;
                }

                yield return null;
            }

            Explode();
        }

        private void CreateExplosionRing()
        {
            var go = new GameObject("[ExplosionRing]");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.widthMultiplier = 0.1f;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = _explosionRingColor;
            lr.endColor = _explosionRingColor;

            int segments = Mathf.Max(3, _explosionRingSegments);
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * _explosionRadius, 0.05f, Mathf.Sin(angle) * _explosionRadius);
                lr.SetPosition(i, point);
            }

            _explosionRing = lr;
        }

        private bool IsOwnerHierarchy(Collider other)
        {
            if (_owner is Component ownerComponent)
                return other.transform.root == ownerComponent.transform.root;
            return false;
        }

        private void Explode()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _explosionRadius, _damageableLayerMask);
            Debug.Log($"[철갑 조각] 폭발 — 위치 {transform.position}, 반경 {_explosionRadius}m, " +
                      $"범위 내 콜라이더 {hits.Length}개 감지");

            foreach (Collider col in hits)
            {
                if (IsOwnerHierarchy(col)) continue;

                if (col.isTrigger) continue;

                IDamageable target = col.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();
                if (target == null) target = col.GetComponentInParent<IDamageable>();

                if (target == null || target.IsDead) continue;

                Vector3 direction = (target.Position - transform.position).normalized;
                var context = new KRDamageContext(_damage, KRDamageType.Metal, transform.position, direction);
                Debug.Log($"[철갑 조각] 폭발 피해 적용 — 대상 {col.name}, " +
                          $"폭발중심과 거리 {Vector3.Distance(transform.position, target.Position):F2}m");
                target.TakeDamage(context);
            }

            Destroy(gameObject);
        }
    }
}
