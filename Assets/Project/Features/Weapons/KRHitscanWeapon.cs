// Assets/Project/Scripts/03_Weapons/KRHitscanWeapon.cs
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Audio;

namespace KillRitual.Weapons
{
    public class KRHitscanWeapon : KRWeaponBase
    {
        private enum KRFireAudioSlot
        {
            AttackType1,
            AttackType2
        }

        [Header("산탄/탄퍼짐")]
        [Tooltip("1회 발사당 생성되는 펠릿(레이) 개수로, 1이면 단발 무기이고 2 이상이면 샷건류이며 KRRampingHitscanWeapon에서는 가속 시작 시점의 펠릿 수로 쓰입니다.")]
        [Min(1)]
        [SerializeField] protected int _pelletCount = 1;

        [Tooltip("탄퍼짐 콘의 전체 각도(도)로, 0이면 완전한 직사이며 KRRampingHitscanWeapon에서는 가속 시작 시점의 탄퍼짐 각도로 쓰입니다.")]
        [Range(0f, 90f)]
        [SerializeField] protected float _spreadAngleDegrees = 0f;

        [Tooltip("탄퍼짐이 있는 무기의 기즈모를 그릴 때 사용하는 박스 높이(시각화 전용, 판정에는 영향 없음)")]
        [Min(0.01f)]
        [SerializeField] private float _boxHeight = 1.5f;

        [Header("총알 꼬리 (트레이서)")]
        [Tooltip("발사 시 생성되는 총알 꼬리 프리팹. LineRenderer + KRHitscanTracer 컴포넌트가 필요합니다. 비워두면 시각효과 없이 발사만 처리됩니다.")]
        [SerializeField] private GameObject _tracerPrefab;

        [Tooltip("트레이서가 화면에서 이동하는 속도(미터/초)로, 클수록 빨리 지나가서 짧게 보이며 충돌 지점 도달 즉시 잔상 없이 사라집니다.")]
        [Min(1f)]
        [SerializeField] private float _tracerVisualSpeed = 250f;

        [Tooltip("트레이서 선 자체의 최대 시각적 길이(미터). 0이면 제한 없이 전체 사거리를 다 그립니다.")]
        [Min(0f)]
        [SerializeField] private float _tracerMaxVisualLength = 8f;

        [Tooltip("이 무기의 트레이서 색상")]
        [SerializeField] private Color _tracerColor = Color.white;

        [Header("오디오")]
        [Tooltip("이 인스턴스가 공격 유형 I/II 중 무엇인지 지정하며, 좌클릭용 무기는 AttackType1, 우클릭용 무기는 AttackType2로 설정합니다.")]
        [SerializeField] private KRFireAudioSlot _fireAudioSlot = KRFireAudioSlot.AttackType1;

        [Tooltip("공격 유형 I 발사음. 예: 화(火) 샷건, 목(木) 정밀소총, 토(土) 기본 연사.")]
        [SerializeField] private AudioClip _attackType1FireClip;

        [Tooltip("공격 유형 II 발사음. 예: 화(火) 슈퍼 샷건, 목(木) 스나이퍼, 토(土) 분쇄기.")]
        [SerializeField] private AudioClip _attackType2FireClip;

        [Tooltip("발사음 볼륨. 최종 크기는 AudioMixer의 SFX 볼륨에도 영향을 받습니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _fireAudioVolume = 1f;

        [Tooltip("발사음 피치 랜덤 범위. 반복 사격 시 완전히 같은 소리로 들리는 것을 줄입니다.")]
        [SerializeField] private Vector2 _fireAudioPitchRange = new Vector2(0.98f, 1.02f);

        [Tooltip("체크하면 화면 중앙에서 나는 2D 사운드로, 해제하면 FirePoint 위치에서 나는 3D 사운드로 출력합니다.")]
        [SerializeField] private bool _playFireAudioAs2D = true;

        // KRRampingHitscanWeapon처럼 코루틴으로 펠릿을 순차 발사할 때 다른 무기 인스턴스가 static 버퍼를 덮어쓰는 문제를 막기 위해 인스턴스 버퍼로 선언합니다.
        private readonly RaycastHit[] _hitscanBuffer = new RaycastHit[16];

        protected override void DoFire(float damagePerPellet)
        {
            Transform fp = ResolveFirePoint();
            if (fp == null) return;

            PlayFireAudio(fp.position);

            int pellets = Mathf.Max(1, GetCurrentPelletCount());
            float spread = GetCurrentSpreadAngle();

            for (int p = 0; p < pellets; p++)
            {
                FireSinglePellet(fp, damagePerPellet, spread, p, pellets);
            }
        }

        protected void PlayFireAudio(Vector3 worldPosition)
        {
            AudioClip clip = ResolveFireAudioClip();
            if (clip == null) return;

            float pitch = ResolveFireAudioPitch();

            if (KRAudioManager.HasInstance)
            {
                if (_playFireAudioAs2D)
                    KRAudioManager.Instance.PlaySFX2D(clip, _fireAudioVolume, pitch);
                else
                    KRAudioManager.Instance.PlaySFXAt(clip, worldPosition, _fireAudioVolume, pitch);

                return;
            }

            // 전역 매니저가 아직 씬에 없을 때 최소한 소리가 나도록 하는 폴백이며, AudioMixer와 pitch는 적용되지 않습니다.
            AudioSource.PlayClipAtPoint(clip, worldPosition, _fireAudioVolume);
        }

        private AudioClip ResolveFireAudioClip()
        {
            return _fireAudioSlot switch
            {
                KRFireAudioSlot.AttackType1 => _attackType1FireClip,
                KRFireAudioSlot.AttackType2 => _attackType2FireClip,
                _ => null
            };
        }

        private float ResolveFireAudioPitch()
        {
            float min = Mathf.Min(_fireAudioPitchRange.x, _fireAudioPitchRange.y);
            float max = Mathf.Max(_fireAudioPitchRange.x, _fireAudioPitchRange.y);

            min = Mathf.Max(0.01f, min);
            max = Mathf.Max(0.01f, max);

            if (Mathf.Approximately(min, max))
                return min;

            return Random.Range(min, max);
        }

        protected void FireSinglePellet(Transform fp, float damage, float spreadAngleDegrees,
            int pelletIndex, int totalPellets)
        {
            Vector3 baseDirection = _combatSystem.GetAimDirection(fp.position, _range);
            Vector3 direction = ComputePelletDirection(baseDirection, spreadAngleDegrees, pelletIndex, totalPellets);

            int hitCount = Physics.RaycastNonAlloc(
                fp.position, direction, _hitscanBuffer, _range, _combatSystem.HitscanLayerMask);

            Vector3 endPoint = ApplyNearestHitDamage(hitCount, damage, fp.position, direction);
            SpawnPelletVisual(fp.position, endPoint);
        }

        protected virtual Vector3 ComputePelletDirection(Vector3 baseDirection, float spreadAngleDegrees,
            int pelletIndex, int totalPellets)
        {
            return ApplySpreadJitter(baseDirection, spreadAngleDegrees);
        }

        protected virtual void SpawnPelletVisual(Vector3 origin, Vector3 endPoint)
        {
            SpawnTracer(origin, endPoint);
        }

        protected virtual int GetCurrentPelletCount() => _pelletCount;

        protected virtual float GetCurrentSpreadAngle() => _spreadAngleDegrees;

        private Vector3 ApplyNearestHitDamage(int hitCount, float damage, Vector3 origin, Vector3 direction)
        {
            int closestIndex = -1;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (_hitscanBuffer[i].distance < closestDistance)
                {
                    closestDistance = _hitscanBuffer[i].distance;
                    closestIndex = i;
                }
            }

            if (closestIndex < 0)
            {
                return origin + (direction * _range);
            }

            RaycastHit hit = _hitscanBuffer[closestIndex];
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

            if (target != null && !target.IsDead && !ReferenceEquals(target, _combatSystem.Owner))
            {
                var context = new KRDamageContext(damage, _element, hit.point, direction);
                target.TakeDamage(context);
            }

            return hit.point;
        }

        protected static Vector3 ApplySpreadJitter(Vector3 forward, float spreadAngleDegrees)
        {
            if (spreadAngleDegrees <= 0f) return forward;

            float halfAngle = spreadAngleDegrees * 0.5f;
            Quaternion jitter = Quaternion.Euler(
                Random.Range(-halfAngle, halfAngle),
                Random.Range(-halfAngle, halfAngle),
                0f);
            return jitter * forward;
        }

        private void SpawnTracer(Vector3 origin, Vector3 endPoint)
        {
            if (_tracerPrefab == null) return;

            GameObject instance = Instantiate(_tracerPrefab, Vector3.zero, Quaternion.identity);

            if (!instance.TryGetComponent(out KRHitscanTracer tracer))
            {
                tracer = instance.AddComponent<KRHitscanTracer>();
            }

            tracer.Play(origin, endPoint, _tracerColor, _tracerVisualSpeed, _tracerMaxVisualLength);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Transform fp = ResolveFirePoint();
            if (fp == null) return;

            Gizmos.color = Color.red;

            if (GetCurrentSpreadAngle() > 0.01f)
                DrawBoxGizmo(fp, GetCurrentSpreadAngle());
            else
                Gizmos.DrawLine(fp.position, fp.position + (fp.forward * _range));
        }

        private void DrawBoxGizmo(Transform fp, float spreadAngle)
        {
            float halfAngleRad = spreadAngle * 0.5f * Mathf.Deg2Rad;
            float width = Mathf.Max(0.05f, _range * Mathf.Tan(halfAngleRad) * 2f);

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(fp.position, fp.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, 0f, _range * 0.5f), new Vector3(width, _boxHeight, _range));
            Gizmos.matrix = originalMatrix;
        }
    }
}