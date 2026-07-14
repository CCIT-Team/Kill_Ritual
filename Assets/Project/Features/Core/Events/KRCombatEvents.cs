//전투시작종료
namespace KillRitual.Core.Events
{
    public readonly struct KRCombatStartEvent
    {
        public readonly int EnemyCount;

        public KRCombatStartEvent(int enemyCount)
        {
            EnemyCount = enemyCount;
        }
    }

    public readonly struct KRCombatEndEvent
    {
    }
}
