// Assets/Project/Scripts/Data/KRElementDataSO.cs
using UnityEngine;
using KillRitual.Core.Damage;

namespace KillRitual.Data
{
    /// <summary>
    /// 공격의 충돌 판정 방식을 정의합니다.
    /// Hitscan/HitscanSpread는 즉발 레이캐스트 계열, Projectile/ExplosiveBurst는 물리 투사체 계열입니다.
    /// </summary>
    public enum KRAttackTypeKind
    {
        /// <summary>단일 레이캐스트 즉발 판정. (목 1/2모드 정밀소총·스나이퍼, 토 1모드 돌격소총)</summary>
        Hitscan,

        /// <summary>다중 펠릿 산탄 레이캐스트 판정. (화 1/2모드 샷건·슈퍼샷건, 토 2모드 체인건)</summary>
        HitscanSpread,

        /// <summary>등속 또는 포물선 운동을 하는 물리 투사체. (수 1/2모드 플라즈마 소총·관통형 플라즈마)</summary>
        Projectile,

        /// <summary>물리 투사체가 충돌 시 광역 폭발 데미지를 발생시킴. (금 1모드 BFG)</summary>
        ExplosiveBurst
    }

    /// <summary>
    /// KRCombatSystem.OnDrawGizmosSelected가 이 값을 보고 어떤 와이어프레임 형태로
    /// 사거리를 시각화할지 결정합니다.
    /// </summary>
    public enum KRGizmoShapeKind
    {
        /// <summary>직선 레이. Hitscan, Projectile 계열의 사거리를 표시합니다.</summary>
        Ray,

        /// <summary>구형. ExplosiveBurst의 폭발 반경을 표시합니다.</summary>
        Sphere,

        /// <summary>사각형 박스. HitscanSpread의 산탄 콘(원뿔)을 각도 기반으로 근사해 표시합니다.</summary>
        Box
    }

    /// <summary>
    /// [DATA 레이어] 무기 1개의 "공격유형(모드)" 한 개에 대한 전체 스펙을 담는 순수 데이터 클래스입니다.
    /// ScriptableObject가 아닌 일반 직렬화 클래스로 두어, KRElementDataSO 하나의 인스펙터 안에서
    /// 공격유형1/공격유형2를 나란히 펼쳐 비교/수정할 수 있게 했습니다.
    /// </summary>
    [System.Serializable]
    public sealed class KRAttackModeData
    {
        [Header("식별")]
        [Tooltip("인스펙터 표시 및 디버그 로그용 이름 (예: \"샷건\", \"슈퍼 샷건\")")]
        public string ModeName = "Mode";

        [Tooltip("충돌 판정 방식")]
        public KRAttackTypeKind AttackType = KRAttackTypeKind.Hitscan;

        [Tooltip("에디터 기즈모 시각화 형태")]
        public KRGizmoShapeKind GizmoShape = KRGizmoShapeKind.Ray;

        [Header("코어 수치")]
        [Tooltip("기본 데미지 (KRCharacterStatsSO.AttackMultiplier가 곱연산됩니다)")]
        [Min(0f)]
        public float Damage = 10f;

        [Tooltip("최대 사거리(미터)")]
        [Min(0.1f)]
        public float Range = 50f;

        [Tooltip("독립 연사 제한시간(초). 다른 무기로 전환해도 이 쿨다운은 화면 뒤에서 계속 흐릅니다.")]
        [Min(0f)]
        public float Cooldown = 0.2f;

        [Tooltip("1회 발사당 소모되는 해당 속성(오행) 공용 자원량")]
        [Min(0f)]
        public float ResourceCost = 5f;

        [Header("산탄/탄퍼짐 (Hitscan 계열 전용)")]
        [Tooltip("HitscanSpread에서 1회 발사당 생성되는 펠릿(레이) 개수. Hitscan 타입은 항상 1로 취급됩니다.")]
        [Min(1)]
        public int PelletCount = 1;

        [Tooltip("탄퍼짐 콘(원뿔)의 전체 각도(도). 0이면 완전한 직사입니다. Box 기즈모의 폭을 계산하는 데도 사용됩니다.")]
        [Range(0f, 90f)]
        public float SpreadAngleDegrees = 0f;

        [Header("물리 투사체 (Projectile / ExplosiveBurst 전용)")]
        [Tooltip("투사체 비행 속도 (미터/초)")]
        [Min(0f)]
        public float ProjectileSpeed = 40f;

        [Tooltip("0 = 완전한 등속 직선 운동(플라즈마 소총류), 0보다 크면 중력의 영향을 받는 포물선 운동")]
        [Min(0f)]
        public float GravityScale = 0f;

        [Tooltip("관통 가능 횟수. 0이면 첫 번째 명중 대상에서 즉시 소멸합니다. (관통형 플라즈마에 사용)")]
        [Min(0)]
        public int PierceCount = 0;

        [Header("광역 폭발 (ExplosiveBurst 전용)")]
        [Tooltip("폭발 반경. 중심부 100% 데미지에서 이 반경 끝에 가까워질수록 0%로 선형 감쇠합니다.")]
        [Min(0f)]
        public float ExplosionRadius = 0f;

        [Header("기즈모 박스 높이 (Box 기즈모 전용)")]
        [Tooltip("Box 기즈모로 시각화할 때의 박스 높이. 폭/길이는 Range와 SpreadAngleDegrees로부터 자동 계산됩니다.")]
        [Min(0.01f)]
        public float BoxHeight = 1.5f;
    }

    /// <summary>
    /// [DATA 레이어] 오행(五行) 속성 무기 1개의 전체 스펙(공격유형1, 공격유형2)을 정의하는
    /// ScriptableObject입니다. 금(金)처럼 공격유형이 하나뿐인 예외 무기는 HasSecondMode를
    /// false로 설정하면, R키/더블탭으로도 모드가 토글되지 않습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "KRElementData_", menuName = "KillRitual/Data/Element Data", order = 1)]
    public sealed class KRElementDataSO : ScriptableObject
    {
        [Tooltip("이 데이터가 어떤 오행 속성에 해당하는지. KRDamageContext.Type과 동일한 enum을 재사용합니다. " +
                 "KRCombatSystem의 _elementDataSet 배열에서 이 값과 동일한 인덱스(int)에 배치되어야 합니다.")]
        public KRDamageType Element = KRDamageType.Fire;

        [Tooltip("UI 무기 아이콘 등에 사용할 스프라이트 (선택 사항)")]
        public Sprite Icon;

        [Tooltip("이 무기가 공격유형 1 ↔ 2 토글을 지원하는지 여부. " +
                 "금(金)/BFG처럼 예외적으로 단일 공격유형만 갖는 무기는 false로 설정합니다.")]
        public bool HasSecondMode = true;

        [Header("공격유형 1")]
        public KRAttackModeData Mode1 = new KRAttackModeData();

        [Header("공격유형 2 (HasSecondMode = false면 사용되지 않음)")]
        public KRAttackModeData Mode2 = new KRAttackModeData();
    }
}
