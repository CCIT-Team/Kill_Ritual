using UnityEngine;
using KillRitual.Core.Damage;

namespace KillRitual.Core.Managers
{
    /// <summary>
    /// Developer B(게임플레이/전투 로직) 담당 파티얼 클래스입니다.
    /// 게임 진행 상태와 데미지 적용 서비스를 정적 프로퍼티로 노출합니다.
    /// </summary>
    public sealed partial class KRManagers : MonoBehaviour
    {
        /// <summary>게임 전체 진행 상태(일시정지 등)를 관리하는 매니저입니다.</summary>
        public static KRGameManager Game { get; private set; }

        /// <summary>단일/AoE 데미지 적용 로직을 일원화한 서비스입니다.</summary>
        public static KRDamageService Damage { get; private set; }

        /// <summary>
        /// Developer B 소관 게임플레이 시스템들을 초기화합니다. KRManagers.Awake()에서 호출됩니다.
        /// </summary>
        private void InitGameplay()
        {
            Game = new KRGameManager();
            Damage = new KRDamageService();
        }
    }
}
