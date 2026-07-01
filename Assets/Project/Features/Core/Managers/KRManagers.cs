using UnityEngine;

namespace KillRitual.Core.Managers
{
    /// <summary>
    /// 전체 매니저들의 메인 허브입니다.
    /// KRManagers.cs / KRManagers.Core.cs / KRManagers.Gameplay.cs 세 파일로 분할하여
    /// Developer A(인프라)와 Developer B(게임플레이)가 서로 다른 파일에서만 작업하도록 함으로써
    /// Git Merge Conflict를 원천적으로 방지합니다. (Partial Managers Splitting 규칙)
    /// </summary>
    public sealed partial class KRManagers : MonoBehaviour
    {
        private static KRManagers _instance;

        /// <summary>
        /// Lazy 싱글톤 인스턴스입니다. 씬에 인스턴스가 없으면 "@KR_Managers"라는 이름의
        /// GameObject를 자동으로 생성하고 DontDestroyOnLoad를 적용합니다.
        /// </summary>
        public static KRManagers Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 씬에 이미 존재할 수 있으므로 먼저 탐색합니다.
                    _instance = FindFirstObjectByType<KRManagers>();

                    if (_instance == null)
                    {
                        var hub = new GameObject("@KR_Managers");
                        _instance = hub.AddComponent<KRManagers>();
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 첫 씬이 로드되기 직전에 강제로 인스턴스를 생성합니다.
        /// 이 메서드가 없으면, KRCombatSystem.OnEnable() 등 다른 스크립트가
        /// KRManagers.Instance가 아니라 KRManagers.Event/Damage 같은 static 프로퍼티를
        /// "직접" 참조할 경우 - 아직 누구도 Instance에 접근한 적이 없어 Awake()가 한 번도
        /// 실행되지 않았다면 - 해당 프로퍼티가 null인 상태로 NullReferenceException이 발생할 수
        /// 있습니다. RuntimeInitializeOnLoadMethod로 게임 시작과 동시에 미리 생성해 두면
        /// 씬 안의 다른 오브젝트들의 실행 순서(Script Execution Order)와 무관하게 항상 안전합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            _ = Instance; // 접근하는 것만으로 생성 + Awake() 즉시 실행이 보장됩니다.
        }

        private void Awake()
        {
            // 씬 전환 등으로 인스턴스가 중복 생성된 경우, 기존 인스턴스를 유지하고 자신은 파괴합니다.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Developer A 담당 영역(이벤트버스, 파일, 풀매니저 등 코어 시스템) 초기화
            InitCore();

            // Developer B 담당 영역(게임 진행, 데미지 서비스 등 게임플레이 시스템) 초기
        }
    }
}
