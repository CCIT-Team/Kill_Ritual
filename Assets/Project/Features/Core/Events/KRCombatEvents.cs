// Assets/Project/Features/Core/Events/KRCombatEvents.cs
//
// [이 파일의 역할]
// "전투 구역(KRCombatZone) 단위의 전투 시작/종료"를 알리는 이벤트 2종을 한 파일에 모아둡니다.
// 원래는 KRCombatStartEvent.cs / KRCombatEndEvent.cs로 파일이 나뉘어 있었으나,
// 이 둘은 항상 KRCombatZone.cs 하나가 함께 발행(Publish)하고 항상 같이 바뀌는 짝꿍 이벤트라서
// 파일을 합쳤습니다. (서로 무관한 다른 이벤트, 예: KRExecutionSuccessEvent는 별도 파일 유지)
//
// [담당자 안내]
// 이 파일과 KRCombatZone.cs(발행 주체)는 한 사람이 같이 관리하는 것을 권장합니다.
// 두 이벤트를 구독하는 곳은 현재 KRDropItem.cs(잔여 자원 정리) 한 곳뿐입니다.
// 새로운 필드를 추가하거나 이벤트를 더 늘릴 경우, 이 파일 상단 주석도 함께 업데이트해주세요.
//
// [발행(Publish) 주체]
// KRCombatZone.cs 단 하나입니다. 다른 스크립트에서 직접 Publish하지 마세요.
//
// [구독(Subscribe) 중인 곳]
// - KRdropitem.cs : KRCombatEndEvent를 구독해, 전투가 끝나면 아직 안 먹힌 잔여 드랍 자원을 제거합니다.
//
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
