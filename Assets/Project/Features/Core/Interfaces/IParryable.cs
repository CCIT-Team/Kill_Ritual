// Assets/Project/Features/Core/Interfaces/IParryable.cs
using UnityEngine;

namespace KillRitual.Core.Interfaces
{
    /// <summary>
    /// [2026-07-07 신규] 무령(패링)으로 막을 수 있는 "예고형 공격"을 가진 오브젝트가
    /// 구현하는 인터페이스입니다. 보스의 3페이즈(공수) 같은 "이 순간에만 패링 가능" 연출에 씁니다.
    ///
    /// [사용 흐름]
    /// 1) 공격 주체(예: KRBossJakdu01)가 예고 동작 중 패링 가능한 짧은 구간에만
    ///    IsParryWindowOpen을 true로 켭니다.
    /// 2) 플레이어가 무령(LCtrl)을 입력하면 KRMuryeongController.TryParry()가 주변의
    ///    IParryable을 찾아 IsParryWindowOpen이 true인 대상에게 OnParried()를 호출합니다.
    /// 3) 공격 주체는 OnParried() 호출 여부로 "막혔는지"를 판단해 이후 분기(그로기/처형 창 오픈 등)를
    ///    처리합니다.
    ///
    /// KRMuryeongController는 지금까지 실제 판정 없이 연출만 재생했는데(패링 성공/실패 개념 자체가
    /// 없었음), 이 인터페이스가 그 판정 로직의 시작점입니다.
    /// </summary>
    public interface IParryable
    {
        /// <summary>지금 이 순간 패링 판정 창이 열려있는지 여부.</summary>
        bool IsParryWindowOpen { get; }

        /// <summary>패링 판정 시 거리 계산에 사용할 월드 좌표.</summary>
        Vector3 Position { get; }

        /// <summary>패링에 성공했을 때 호출됩니다. 창이 닫혀있을 때는 호출되지 않습니다.</summary>
        void OnParried();
    }
}
