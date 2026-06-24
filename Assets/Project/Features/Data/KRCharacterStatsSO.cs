// Assets/Project/Scripts/Data/KRCharacterStatsSO.cs
using UnityEngine;

namespace KillRitual.Data
{
    /// <summary>
    /// [DATA 레이어] 플레이어 캐릭터의 기본 스탯을 정의하는 ScriptableObject입니다.
    /// 이 파일은 순수 데이터만 담으며, 어떠한 게임 로직(계산/판정)도 포함하지 않습니다.
    /// 데이터-로직 이원화 원칙에 따라, LOGIC 레이어인 KRCombatSystem이 이 데이터를
    /// "참조"하여 사용할 뿐, 이 SO는 자기 자신을 어떻게 쓰는지 전혀 알지 못합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "KRCharacterStats", menuName = "Kill Ritual/Data/Character Stats", order = 0)]
    public sealed class KRCharacterStatsSO : ScriptableObject
    {
        [Header("생존")]
        [Tooltip("플레이어의 최대 체력")]
        [Min(1f)]
        public float MaxHealth = 100f;

        [Header("기동")]
        [Tooltip("이동 가중치. Developer B의 무브먼트 스크립트가 기본 이동 속도에 곱연산으로 적용하는 배율입니다.")]
        [Min(0.01f)]
        public float MoveWeight = 1f;

        [Header("전투")]
        [Tooltip("모든 무기 데미지에 곱연산으로 적용되는 전역 공격 배율입니다.")]
        [Min(0.01f)]
        public float AttackMultiplier = 1f;

        [Tooltip("무기별 독립 쿨다운에 나눗셈으로 적용되는 공격 속도 배율입니다. 값이 클수록 같은 무기를 더 빠르게 연사할 수 있습니다.")]
        [Min(0.01f)]
        public float AttackSpeedMultiplier = 1f;
    }
}
