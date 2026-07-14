using KillRitual.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillRitual.StagePortal
{
    public class KRStageLauncher : MonoBehaviour
    {
        public enum LoadMode
        {
            DirectSceneLoad,
            LoadingScene
        }

        [Header("Load Mode")]
        [SerializeField] private LoadMode loadMode = LoadMode.LoadingScene;

        [Header("Loading Scene")]
        [SerializeField] private string loadingSceneName = "LoadingScene";

        public void Launch(KRStageData stageData)
        {
            if (stageData == null)
            {
                Debug.LogWarning($"{nameof(KRStageLauncher)}: StageData가 없습니다.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(stageData.SceneName))
            {
                Debug.LogWarning($"{nameof(KRStageLauncher)}: SceneName이 비어 있습니다.", stageData);
                return;
            }

            switch (loadMode)
            {
                case LoadMode.DirectSceneLoad:
                    SceneManager.LoadScene(stageData.SceneName);
                    break;

                case LoadMode.LoadingScene:
                    // 기존 씬 전환 데이터 구조 재사용.
                    KRSceneTransitionData.SetGameStart(
                        targetSceneName: stageData.SceneName,
                        startMode: KRGameStartMode.NewGame,
                        saveSlotId: null
                    );

                    SceneManager.LoadScene(loadingSceneName);
                    break;
            }
        }
    }
}