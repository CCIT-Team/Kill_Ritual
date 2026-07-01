// Assets/Project/Scripts/01_Core/Damage/KRDamageContext.cs
using UnityEngine;

namespace KillRitual.Core.Damage
{
    /// <summary>
    /// 동양 오행(五行) 속성 5종을 정의하는 열거형입니다.
    /// KRDamageContext.Type(피격 시 속성)과 각 무기 스크립트(KRWeaponBase._element)의 속성이
    /// 동일한 이 enum을 공유하여 사용합니다. 무기의 속성이 곧 그 무기가 입히는 데미지의 속성과
    /// 같기 때문에, 별도의 "원소 타입" enum을 새로 만들지 않고 하나로 통합했습니다.
    /// 캐스팅 안전성을 위해 정수 값을 명시적으로 고정합니다.
    /// </summary>
    public enum KRDamageType
    {
        /// <summary>화(火) - 근·중거리 폭딜 (샷건/슈퍼샷건)</summary>
        Fire = 0,

        /// <summary>수(水) - 방어막 파괴/유틸리티 (플라즈마 소총/관통형 플라즈마)</summary>
        Water = 1,

        /// <summary>목(木) - 원거리 정밀 제거 (정밀 소총/스나이퍼)</summary>
        Wood = 2,

        /// <summary>토(土) - 전선 유지 연사 (돌격소총/체인건)</summary>
        Earth = 3,

        /// <summary>금(金) - 최종 병기 (BFG, 단일 공격유형 예외)</summary>
        Metal = 4
    }

    /// <summary>
    /// 데미지 발생 시 전달되는 불변(immutable) 값 타입입니다.
    /// 데미지 계산, 이벤트 전파처럼 매 프레임 빈번하게 발생할 수 있는 연산에서
    /// class(참조 타입) 대신 struct(값 타입)를 사용하여 힙 할당을 막고
    /// GC(가비지 컬렉터) 스파이크를 방지합니다.
    /// 모든 필드는 readonly이며, 생성자를 통해서만 값을 설정할 수 있습니다.
    /// </summary>
    public readonly struct KRDamageContext
    {
        /// <summary>최종 적용될 데미지 양.</summary>
        public readonly float DamageAmount;

        /// <summary>데미지 속성 (火/水/木/土/金 중 하나).</summary>
        public readonly KRDamageType Type;

        /// <summary>피격 지점의 월드 좌표. 이펙트 생성, AoE 중심점 계산 등에 사용됩니다.</summary>
        public readonly Vector3 HitPoint;

        /// <summary>데미지가 들어온 방향. 히트 리액션 연출 등에 사용됩니다.</summary>
        public readonly Vector3 Direction;

        public KRDamageContext(float damageAmount, KRDamageType type, Vector3 hitPoint, Vector3 direction)
        {
            DamageAmount = damageAmount;
            Type = type;
            HitPoint = hitPoint;
            Direction = direction;
        }
    }
}