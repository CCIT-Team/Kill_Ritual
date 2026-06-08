using UnityEngine;

namespace KillRitual.StagePortal
{
    [CreateAssetMenu(
        fileName = "KRStageData_",
        menuName = "Kill Ritual/Stage/Stage Data"
    )]
    public class KRStageData : ScriptableObject
    {
        [Header("Scene")]
        [SerializeField] private string stageId;
        [SerializeField] private string sceneName;

        [Header("Display")]
        [SerializeField] private string displayName;
        [TextArea(3, 8)]
        [SerializeField] private string overview;

        [Header("Optional UI")]
        [SerializeField] private Sprite stageIcon;
        [SerializeField] private string difficultyText;
        [SerializeField] private string rewardText;

        [Header("State")]
        [SerializeField] private bool unlocked = true;

        public string StageId => stageId;
        public string SceneName => sceneName;
        public string DisplayName => displayName;
        public string Overview => overview;
        public Sprite StageIcon => stageIcon;
        public string DifficultyText => difficultyText;
        public string RewardText => rewardText;
        public bool Unlocked => unlocked;
    }
}