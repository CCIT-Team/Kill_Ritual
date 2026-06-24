namespace KillRitual.UI
{
    public enum KRGameStartMode
    {
        NewGame,
        Continue,
        LoadSlot
    }

    public static class KRSceneTransitionData
    {
        public static string TargetSceneName { get; private set; }
        public static KRGameStartMode StartMode { get; private set; }
        public static string SaveSlotId { get; private set; }

        public static bool HasTargetScene => !string.IsNullOrWhiteSpace(TargetSceneName);

        public static void SetGameStart(
            string targetSceneName,
            KRGameStartMode startMode = KRGameStartMode.NewGame,
            string saveSlotId = null)
        {
            TargetSceneName = targetSceneName;
            StartMode = startMode;
            SaveSlotId = saveSlotId;
        }

        public static void Clear()
        {
            TargetSceneName = null;
            StartMode = KRGameStartMode.NewGame;
            SaveSlotId = null;
        }
    }
}