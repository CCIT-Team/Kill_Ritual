using UnityEngine;
using KillRitual.Core.Events;
using KillRitual.Core.SaveData;

namespace KillRitual.Core.Managers
{
    /// <summary>
    /// Developer A(인프라/무기/코어 시스템) 담당 파티얼 클래스입니다.
    /// 이벤트버스, 파일 매니저, 오브젝트 풀 매니저 등 "코어 시스템"을 정적 프로퍼티로 노출하여
    /// 어디서든 KRManagers.Event, KRManagers.File, KRManagers.Pool 형태로 접근할 수 있게 합니다.
    /// </summary>
    public sealed partial class KRManagers : MonoBehaviour
    {
        /// <summary>전역 이벤트 버스. UI와 Player/Weapon 사이의 디커플링을 담당합니다.</summary>
        public static KREventBus Event { get; private set; }

        /// <summary>
        /// 세이브/로드를 담당하는 파일 매니저입니다.
        /// 01_Core/SaveData/KRFileManager.cs에 구현된 JSON 기반 범용 세이브/로드 클래스를 연결합니다.
        /// </summary>
        public static KRFileManager File { get; private set; }

        /// <summary>발사체/이펙트 재사용을 위한 오브젝트 풀 매니저(현재는 최소 스텁).</summary>
        public static KRPoolManager Pool { get; private set; }

        /// <summary>
        /// Developer A 소관 코어 시스템들을 초기화합니다. KRManagers.Awake()에서 호출됩니다.
        /// </summary>
        private void InitCore()
        {
            Event = new KREventBus();
            File = new KRFileManager();
            Pool = new KRPoolManager();
        }
    }
}
