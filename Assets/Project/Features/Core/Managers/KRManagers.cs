using UnityEngine;

namespace KillRitual.Core.Managers
{
    public sealed partial class KRManagers : MonoBehaviour
    {
        private static KRManagers _instance;

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
