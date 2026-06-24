using UnityEngine;

namespace KillRitual.Core.Managers
{
    /// <summary>
    /// [보조 파일] 게임 전체 상태(일시정지, 추후 라운드/스테이지 진행 등)를 관리하는 매니저입니다.
    /// 명세에서 KRManagers.Gameplay.cs가 "KRGameManager Game" 프로퍼티를 노출하도록 요구했지만
    /// 해당 클래스 자체는 별도 파일로 명시되지 않아, Single File Mandate를 지키기 위해
    /// 이 파일에서 최소 기능으로 구현했습니다. Developer B가 자유롭게 확장할 수 있습니다.
    /// </summary>
    public sealed class KRGameManager
    {
        /// <summary>현재 게임이 일시정지 상태인지 여부.</summary>
        public bool IsPaused { get; private set; }

        public KRGameManager()
        {
            IsPaused = false;
        }

        /// <summary>게임을 일시정지하거나 재개합니다.</summary>
        public void SetPause(bool isPaused)
        {
            IsPaused = isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
        }
    }
}
