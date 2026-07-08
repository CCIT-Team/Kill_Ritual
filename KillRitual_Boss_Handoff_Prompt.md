# Kill Ritual — 보스(불가살이) 작업 인계 프롬프트

아래는 Unity C# 게임 "Kill Ritual"의 1스테이지 보스 재설계 작업 컨텍스트를 압축한 것입니다.
이 내용을 그대로 시스템/첫 메시지로 사용해서 이어서 작업하면 됩니다.

## 0. 프로젝트 기본 정보

- 저장소: `C:\Users\johs5\GitHub\Kill_Ritual` (GitHub Desktop 관리, 브랜치 `feature_hs`)
- 대상: 1스테이지 보스, 클래스/파일명은 `KRBossJakdu01`("작두 보스" 시절 이름 그대로 유지 — 씬/프리팹이 이미 이 이름을 참조 중이라 바꾸면 GUID 연결이 꼬릴 위험이 있어 유지). 실제 컨셉명은 "불가살이".

## 1. 반드시 지킬 규칙 (Standing Rules)

- 모든 스크립트 주석은 한국어로 작성.
- 코드를 변경하면 파일/줄/함수 단위로 한국어로 설명.
- 파일이나 내용을 삭제하기 전엔 반드시 먼저 한국어로 이유를 설명.
- 수정한 파일을 요청받으면 전체 코드를 출력.
- `.unity` 씬 파일은 명시적 허락 없이 절대 건드리지 않음.
- 연결된 워크스페이스 폴더의 파일은 자동으로 삭제/이름변경 불가 — 지워야 할 게 있으면 반드시 먼저 물어보고 허락받을 것.

## 2. 현재 설계 컨셉 — "부위타격(파트 브레이크)" 시스템

기존의 "불가살이"(평소 거의 무적, 특정 패턴 직후에만 잠깐 노출) 컨셉을 버리고, 몬스터헌터류의
"부위별 체력 + 파괴(break)" 시스템으로 전면 교체함. 새 모델(`Assets/bulgasari/Four Legged
Predator.fbx`)이 머리/몸통/다리 텍스처가 이미 부위별로 나뉘어 있어서 그 구분을 그대로
게임플레이에 살림.

핵심 규칙:
- 부위는 **항상** 맞을 수 있음. "패턴 끝난 뒤에만 노출" 같은 시간제한 노출 창 개념은 완전히 삭제됨.
- 부위 구성 5개: 머리(Head) / 몸통(Body) / 앞다리(FrontLegs) / 뒷다리(BackLegs) / 꼬리(Tail).
- 부위마다 자기 체력(`_partHealth`)이 있고, 0이 되면 "파괴"(`_isBroken=true`)되며 실제 전투에
  영향을 주는 행동 변화가 걸림:
  - 다리(앞/뒤) 파괴 시: 이동속도 감소(누적곱), 돌진 패턴 봉인, 강제 다운(그로기).
  - 돌진 자체도 부위 파괴와 연동: 벽에 부딪히면 그 충격으로 앞다리 자신에게 자해 피해(무리한
    돌진 반복 시 스스로 다리가 부러질 수 있는 리스크/리워드).
- 부위 타격은 항상 보스 본체 체력에도 동시에 데미지를 줌(부위 체력 + 본체 체력 이중 처리).

## 3. 새 모델 정보 (Four Legged Predator.fbx)

- Blender Rigify 쿼드러페드 스켈레톤. 디폼 본 접두사 `DEF-`.
- 실제 애니메이션 클립 7종(AnimStack `rig|<이름>`): `Idle`, `walk`, `Run`, `attack`,
  `Powerfull_attack`, `Roar`, `Sleeping`(전투에 안 씀, 컨트롤러에서 제외).
- FBX meta: `avatarSetup: 1`, `animationType: 2`(Generic). **주의**: 클립이 실제로
  분리(split)됐는지, Avatar가 정상 생성됐는지는 에디터에서 직접 확인 필요 — 마지막 확인 시점엔
  `clipAnimations: []`로 비어있었음(Unity가 아직 처리 안 한 상태일 수 있음).

## 4. 핵심 파일 목록 및 상태

| 파일 | 상태 |
|---|---|
| `Assets/Project/Features/Enemies/MakeNew/KRBossBodyPart.cs` | 부위 스크립트. `_partHealth`, `_isBroken`, `OnBroken` 이벤트. 전면 재작성 완료. |
| `Assets/Project/Features/Enemies/MakeNew/KRBossJakdu01.cs` | 보스 메인 로직. 전면 재작성 + 이번 세션에 추가 수정(아래 5번 참고). |
| `Assets/Project/Prefabs/Monster/Boss/KRBossJakdu01.prefab` | 처음부터 새로 제작(기존 파일 사용자가 실수로 삭제해서 복구 대신 재설계 선택). **현재 사용자가 Unity 에디터에서 직접 실시간 편집 중** — 부위 콜라이더들을 실제 스켈레톤 본에 재부모화하는 등. |
| `Assets/Project/Prefabs/Monster/Boss/KRBossArmorShard.prefab` | 철갑 조각 발사체. 레이캐스트 기반(Collider 없음), guid 기존 것 재사용. |
| `Assets/Project/Prefabs/Monster/Boss/불가사리.controller` | **신규** AnimatorController (한글 이름, 사용자 요청). 옛 `KRBossMastodon.controller`는 파일 동기화 문제로 손상되어 사용 중단, 대신 이 파일 새로 만듦. 6개 State(Idle/Walk/Run/Attack/PowerfulAttack/Roar), Motion 필드는 전부 `{fileID: 0}`(비어있음 — 에디터에서 실제 클립 드래그 필요). |
| `Assets/Project/Prefabs/Monster/Boss/KRBossMastodon.controller` | **오래돼서 안 씀(orphaned)**. 아무도 참조 안 함. 삭제해도 되는지 사용자 확인 대기 중. |
| `Assets/Project/Prefabs/Monster/Boss/KRWeakPointGlow.mat` | 예전 노출창 시스템용 머티리얼. 새 설계에선 사실상 안 씀. |

## 5. 이번 세션에 완료한 코드 수정 (KRBossJakdu01.cs)

1. **전력 질주(Sprint)**: `_sprintDistanceMultiplier`(기본 2배), `_sprintSpeedMultiplier`(기본
   1.6배) 필드 추가. 플레이어가 `_preferredDistance × _sprintDistanceMultiplier`보다 멀어지면
   `_agent.speed`를 실제로 올려서 더 빨리 쫓아옴(다리 파괴 감속과 곱셈으로 함께 적용됨,
   `_legSpeedMultiplier`로 관리).
2. **패턴 중 재조준 정지**: `UpdateChase()`/`UpdateAttack()`에서 `_isPatternActive`일 때는
   `FacePlayer()`를 호출 안 함. 예전엔 매 프레임 무조건 조준해서, 회전속도를 낮춰도 결국 항상
   정면이 플레이어를 향했음. 이제 패턴(예고~공격) 도중엔 방향을 유지 → 플레이어가 옆/뒤로 돌아갈
   실제 시간적 여유가 생김.
3. **꼬리 휘두르기 범위 버그 수정**: `TryHitTrunkStrike()`가 원래 꼬리(`_tail`) 위치를 기준점으로
   쓰면서 각도까지 "몸 뒤쪽만"으로 제한했었는데, 이러면 평소처럼 정면에서 쫓기다 이 패턴에 걸리는
   일반적 상황에서 사거리 안에 있어도 거의 항상 안 맞는 버그였음. 각도 제한 삭제, 꼬리 위치 기준
   순수 원형 범위 판정으로 단순화했음. (`_trunkStrikeHalfAngle` 필드는 남겨뒀지만 현재 미사용.)
4. **돌진 후 재조준 2초 고정**: `_chargeTurnBackDuration`(기본 2초) 필드 + `TurnBackTowardsPlayer()`
   코루틴 신규 추가. 돌진(+벽충돌 시 경직/2연속돌진)이 끝나면 남은 각도와 무관하게 항상 정확히
   지정한 시간 동안 Slerp로 부드럽게 플레이어 쪽으로 돌아봄. `Pattern_Charge()` 맨 끝에서 호출.
5. **Walk/Run 애니메이션 확인**: 현재 코드는 `Speed` 파라미터로 0(Idle)/2(Run) 두 값만 씀.
   컨트롤러엔 Walk 상태/전환이 있지만 코드에서 중간값을 절대 안 넣어서 실질적으로 미사용 상태
   (의도된 것인지 확인 필요 — 필요하면 근거리 접근 시 Walk 쓰는 조건 추가 가능).

## 6. 아직 안 풀린 문제 / 다음 할 일

1. **애니메이션이 실제로 적용 안 되는 문제**: 프리팹 파일을 직접 확인한 결과, Animator 컴포넌트
   자체가 (중첩된 FBX 프리팹 인스턴스 루트에 자동 생성됐을 가능성은 있으나) Controller 필드가
   비어있는(None) 상태로 추정됨. **에디터 작업 필요**: 모델 오브젝트의 Animator 컴포넌트에
   `불가사리.controller`를 Controller로 지정하고 Avatar도 확인할 것.
2. **발사체(철갑 조각)가 안 보이는 문제**: `_shoulderLMuzzle`/`_shoulderRMuzzle` 필드가 프리팹에서
   둘 다 `{fileID: 0}`(미연결) 상태. `FireShardsFromMuzzle()`은 muzzle이 null이면 조용히
   아무것도 안 함. **에디터 작업 필요**: 양쪽 어깨 뼈(`DEF-shoulder` 근처) 밑에 빈 GameObject를
   만들어 위치 잡고, 스크립트의 두 필드에 드래그해서 연결할 것.
3. **`불가사리.controller`의 Motion 필드 6개가 전부 비어있음**: 에디터에서 FBX의 실제 클립
   (Idle/walk/Run/attack/Powerfull_attack/Roar)을 각 State의 Motion으로 드래그해서 채워야 함.
4. **Apply Root Motion 켜짐 상태**: 사용자가 켰음. `MoveTowards()`(NavMeshAgent 기반 이동)와
   충돌할 위험이 있음(애니메이션이 매 프레임 위치를 덮어쓸 수 있음) — 실제로 이동이 깨졌는지
   아직 확인 안 됨. 문제 생기면: (a) 토글 끄기, 또는 (b) `OnAnimatorMove()` + `_agent.nextPosition`
   동기화 코드 구현.
5. **오래된 `KRBossMastodon.controller` 삭제 여부**: 사용 안 하는 게 확인됐지만(guid 참조하는 곳
   없음), 삭제하려면 사용자에게 먼저 물어봐야 함(아직 미확인).
6. **`_shoulderLMuzzle`/`_shoulderRMuzzle` 외에 프리팹 전반**: 사용자가 Unity 에디터에서 직접
   실시간으로 부위 콜라이더들을 실제 스켈레톤 본에 재부모화하는 등 계속 편집 중이므로, 프리팹
   파일을 다시 읽어서 현재 실제 연결 상태(`_head`/`_body`/`_frontLegs`/`_backLegs`/`_tail`/
   `_visualAnimator`/`_chargeHitbox` 등)를 확인한 뒤 작업할 것.

## 7. 이번 세션에서 겪은 특이사항 (참고용)

- 프리팹/컨트롤러 파일을 Write/Edit할 때 "File has been modified since read" 오류나, Read
  결과와 실제 Unity/bash가 보는 내용이 다른 파일 동기화 문제를 여러 번 겪음. 해결책: 문제가
  생긴 파일은 억지로 고치려 하지 말고, 필요하면 새 파일로 다시 만드는 게 더 안전함(실제로
  `KRBossMastodon.controller` → `불가사리.controller`로 이렇게 해결함).
- 사용자는 Unity 에디터를 열어놓고 프리팹/머티리얼을 실시간으로 직접 편집 중일 수 있음. 사용자의
  실시간 편집 내용은 되돌리지 말 것.
