// Assets/Project/Features/Enemies/MakeNew/KRBossArmorShard.cs
using System.Collections;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// [2026-07-07 신규] 불가살이 보스의 "철갑 발사"(1페이즈 패턴1) 전용 투사체입니다.
    ///
    /// [2026-07-08 전면 재작성 — 레이캐스트 → 트리거 콜라이더 방식으로 변경]
    /// 원래는 KRPhysicsProjectile과 같은 이유로 레이캐스트로 직접 이동/충돌을 계산했습니다
    /// (Collider가 있는 채로 물리 충돌을 켜두면 PhysX가 플레이어를 밀어내는 부작용 우려).
    /// 그런데 실제로 겪어보니 레이캐스트 방식은 LayerMask 설정(Everything이면 자기 자신도
    /// 맞음), Combat 레지스트리 등록 여부, 플레이어 쪽 별도 Collider의 Enabled 상태 등
    /// 여러 군데가 한 군데라도 어긋나면 조용히 안 맞는 문제가 반복적으로 생겼습니다.
    /// "그냥 투사체 공격으로 하자"는 요청 반영 — Collider를 IsTrigger로 켜고(물리적 충돌/밀림은
    /// 절대 안 일어남), Rigidbody도 Kinematic으로 붙여서(트리거 이벤트가 안정적으로 발생하도록)
    /// OnTriggerEnter 기반으로 바꿨습니다. KRBossChargeHitbox와 똑같은 검증된 방식이라 Combat
    /// 레지스트리에도 안 기대고 GetComponentInParent로 직접 찾습니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class KRBossArmorShard : MonoBehaviour
    {
        private Rigidbody _rigidbody;

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

        /// <summary>
        /// 발사 직후 보스 컨트롤러가 호출해 이 투사체를 초기화합니다.
        /// </summary>
        /// <param name="velocity">초기 속도(방향 포함).</param>
        /// <param name="damage">플레이어를 직접 맞췄을 때(또는 폭발 시) 주는 피해량.</param>
        /// <param name="hitLayerMask">비행 중 충돌을 감지할 레이어(플레이어+환경 포함).</param>
        /// <param name="damageableLayerMask">폭발 판정에 쓸 레이어(피격 가능 대상만).</param>
        /// <param name="owner">발사한 주체(보스 자기 자신에게는 맞지 않도록 제외).</param>
        /// <param name="willExplode">true면 바닥/벽에 꽂힌 뒤 explodeDelay초 후 폭발합니다(2페이즈용).</param>
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
            // [2026-07-08 신규] Continuous로 해두면 빠르게 움직이는 작은 콜라이더가 얇은 벽을
            // 그냥 통과해버리는(터널링) 문제를 줄일 수 있습니다.
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

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

            // [2026-07-08 변경] Kinematic Rigidbody는 transform.position을 직접 바꾸는 대신
            // MovePosition()으로 옮겨야 트리거 이벤트가 프레임 사이에서도 안정적으로 발생합니다.
            _rigidbody.MovePosition(_rigidbody.position + _velocity * Time.fixedDeltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_stuck) return;

            // [2026-07-08 신규 — "생겼다가 그 자리에 멈춤" 버그의 실제 원인]
            // 철갑 발사 패턴은 한쪽 어깨에서 여러 발(기본 3발)을 "같은 muzzle.position"에서
            // 방향만 다르게 동시에 Instantiate합니다. 그러면 스폰되는 순간부터 이 조각들의
            // SphereCollider끼리 완전히 겹쳐 있는 상태라, 첫 물리 스텝에 서로를 OnTriggerEnter로
            // 맞닥뜨립니다. 상대는 IDamageable이 아니고(다른 KRBossArmorShard이므로) 자기 자신은
            // 보스 소유물이지만 서로는 보스 계층의 자식이 아니라 각자 독립된 루트 오브젝트라
            // IsOwnerHierarchy 체크도 통과하지 못해서 "그냥 벽에 맞은 것"으로 처리되어 그 자리에서
            // 즉시 멈춰버렸습니다. 다른 철갑 조각끼리는 아예 판정 대상에서 제외해 원천 차단합니다.
            if (other.GetComponentInParent<KRBossArmorShard>() != null) return;

            // [2026-07-08 신규] hitLayerMask에 안 걸리는 대상(예: 다른 투사체, 이펙트 전용 레이어 등)은
            // 아예 무시합니다 — 레이캐스트 시절의 hitLayerMask 필터링을 그대로 이어받은 것입니다.
            if (((1 << other.gameObject.layer) & _hitLayerMask.value) == 0) return;

            // [2026-07-08 신규] 발사 위치(어깨 머즐)가 보스 자신의 부위 콜라이더 바로 옆이라, 맨 첫
            // 프레임에 자기 자신의 Head/Body 등 부위 콜라이더를 트리거로 맞힐 수 있습니다.
            // ReferenceEquals(target, _owner)만으로는 "부위 컴포넌트 != 보스 본체 컴포넌트"라
            // 못 걸러지므로, 계층구조 루트(transform.root)가 발사 주체와 같은지로 직접 판정합니다.
            if (IsOwnerHierarchy(other)) return;

            // [2026-07-08 신규 — "맞지 않았는데 맞았다고 뜬다" 버그의 실제 원인]
            // 로그로 확인해보니 실제로 맞은 콜라이더가 플레이어 몸이 아니라 "Absortion Collider"
            // (Player/CameraRoot 하위의 아이템 자동 흡수용 콜라이더, 스케일이 15×13×23m나 되는
            // 트리거)였습니다. GetComponentInParent는 어떤 콜라이더가 맞았는지 상관없이 그 위쪽
            // 계층에 KRPlayerDamageFeedback만 있으면 다 "플레이어를 맞췄다"로 처리해버려서,
            // 실제로는 몸에서 4m 넘게 떨어진 흡수 범위 트리거에 스친 것도 정면 피격으로 잡혔습니다.
            // 진짜 몸(피지컬) 콜라이더는 트리거가 아니므로(CharacterController), 트리거 콜라이더는
            // 아예 "몸에 닿은 것"으로 취급하지 않고 그냥 통과시킵니다 — 흡수 범위/감지 존 같은
            // 게임플레이용 트리거들과 실제 히트박스를 명확히 구분하기 위함입니다.
            if (other.isTrigger) return;

            Vector3 point = other.ClosestPoint(transform.position);

            // [2026-07-08 변경] KRBossChargeHitbox와 동일한 우선순위 — Combat 레지스트리에 기대지
            // 않고 GetComponentInParent로 직접 찾습니다(플레이어가 그 레지스트리에 등록되어 있지
            // 않아서 겪었던 문제를 원천적으로 피합니다).
            IDamageable target = other.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();
            if (target == null) target = other.GetComponentInParent<IDamageable>();

            if (target != null && !target.IsDead)
            {
                // [2026-07-08 신규] 판정 순간 철갑/상대 콜라이더의 실제 위치를 같이 남깁니다 —
                // KRPlayerDamageFeedback.TakeDamage() 로그와 대조하면 "판정이 눈에 보이는 것보다
                // 넓다"류의 문제를 재현 없이 로그만으로 확인할 수 있습니다.
                Debug.Log($"[철갑 조각] 직접 명중 — 철갑 위치 {transform.position}, " +
                          $"충돌지점 {point}, 대상 콜라이더({other.name}) 중심 {other.transform.position}, " +
                          $"철갑-대상중심 거리 {Vector3.Distance(transform.position, other.transform.position):F2}m, " +
                          $"콜라이더 반지름 {(other as SphereCollider)?.radius}");

                // 플레이어(또는 다른 피격 대상)를 직접 맞췄으면 즉시 피해를 주고 사라집니다.
                var context = new KRDamageContext(_damage, KRDamageType.Metal, point, _velocity.normalized);
                target.TakeDamage(context);
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

        /// <summary>
        /// [2026-07-08 신규] 폭발까지 남은 시간 동안 바닥에 폭발 반경 원을 그려서 보여주고,
        /// 터지기 직전으로 갈수록 점점 빠르게 깜빡/확대되도록 만듭니다("맞지 않아도 죽는다"
        /// 문제의 실제 원인이었던 무예고 폭발을 없애는 게 목적입니다).
        /// </summary>
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

        /// <summary>폭발 반경(_explosionRadius)만큼 바닥에 원을 그리는 LineRenderer를 생성합니다.</summary>
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

        /// <summary>
        /// [2026-07-08 신규] 발사 주체(_owner)와 같은 오브젝트 계층(transform.root)에 속하는
        /// 콜라이더인지 판정합니다. _owner가 MonoBehaviour(Component)가 아니면(이론상 없지만
        /// 안전하게) false를 반환해 판정을 건너뛰지 않습니다.
        /// </summary>
        private bool IsOwnerHierarchy(Collider other)
        {
            if (_owner is Component ownerComponent)
                return other.transform.root == ownerComponent.transform.root;
            return false;
        }

        /// <summary>2페이즈 전용 — 꽂힌 자리에서 지연 폭발해 주변에 광역 피해를 줍니다.</summary>
        private void Explode()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _explosionRadius, _damageableLayerMask);
            Debug.Log($"[철갑 조각] 폭발 — 위치 {transform.position}, 반경 {_explosionRadius}m, " +
                      $"범위 내 콜라이더 {hits.Length}개 감지");

            foreach (Collider col in hits)
            {
                if (IsOwnerHierarchy(col)) continue;

                // [2026-07-08 신규] 직격 판정과 동일한 이유로, 트리거 콜라이더(예: 거대한 아이템
                // 자동흡수 범위)는 "몸에 폭발 피해를 입을 대상"에서 제외합니다. 안 그러면 폭발
                // 반경(_explosionRadius)보다 훨씬 넓은 범위에서도 맞은 것으로 처리될 수 있습니다.
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
