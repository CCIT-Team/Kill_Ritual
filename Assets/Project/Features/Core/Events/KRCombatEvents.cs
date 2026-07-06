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
    /// <summary>
    /// 전투 구역(KRCombatZone)에 플레이어가 진입해 전투가 "시작"됐음을 알리는 이벤트.
    ///
    /// [발행 시점] KRCombatZone.TryStartCombat() — 플레이어가 존(Box Collider Trigger)에
    /// 들어온 순간, 존 범위 안에서 살아있는 적을 1마리 이상 찾았을 때 딱 한 번 발행합니다.
    /// (적이 하나도 없는 빈 구역에 들어가면 발행되지 않습니다.)
    ///
    /// [활용 예시] 전투 시작 UI 연출, 배경음악을 전투 테마로 전환 등에 구독해서 쓸 수 있습니다.
    /// 현재는 아직 이 이벤트를 구독하는 곳이 없습니다(전투 종료 쪽만 KRDropItem이 사용 중).
    /// </summary>
    public readonly struct KRCombatStartEvent
    {
        /// <summary>이번 전투에 참가하는(=존 진입 시점에 이 구역에서 감지된) 적의 수.</summary>
        public readonly int EnemyCount;

        public KRCombatStartEvent(int enemyCount)
        {
            EnemyCount = enemyCount;
        }
    }

    /// <summary>
    /// 전투 구역(KRCombatZone) 안에서 KRCombatStartEvent 발행 시점에 등록됐던 적이
    /// 전부 사망(IsDead)해 전투가 "종료"됐음을 알리는 이벤트.
    ///
    /// [발행 시점] KRCombatZone.CheckForCombatEnd() — Update()에서 0.5초(기본값)마다
    /// 참가자 전멸 여부를 체크하다가, 전멸한 바로 그 체크 타이밍에 딱 한 번 발행합니다.
    ///
    /// [활용 예시] 지금은 KRDropItem.cs가 이 이벤트를 구독해서, 작두 등으로 드롭됐지만
    /// 아직 플레이어가 못 주운 잔여 탄약/체력 오브를 이 시점에 제거합니다
    /// (기획서 3-5/4-4/5-2 "잔여 자원은 전투 종료 시 제거" 규칙 반영).
    /// 필드가 없는 빈 구조체입니다 — "종료됐다"는 사실 자체만 알리면 충분하기 때문입니다.
    /// </summary>
    public readonly struct KRCombatEndEvent
    {
    }
}
