# Architecture

## 현재 구성 — Scene 소유 루트·TMP·타일 통행 분류 — 2026-10-20

최신 사용자 지시에 따라 GameRoot는 Game.unity 소유 일반 객체이며 바깥 Prefab 자산은 제거했다. 중첩 공간/캐릭터/UI Prefab은 재사용 단위로 유지한다. 과거 공통 GameRoot.prefab 중심 설명보다 이 안내와 [ReconstructionPlan18절](ReconstructionPlan.md)의 실제 결과가 우선한다. 기획/새 개발 표준은 바꾸지 않았다.

UI32개 Prefab은 TMP_Text(TextMeshProUGUI)/TMP_InputField와 사전 직렬화된 NotoSansKR SDF를 사용한다. OS 폰트 런타임 생성은 제거했다. 거래/대행인/고용 Frame 및 상품 행은 도안에 맞춰 재제작했고 기존 경제 규칙을 연결한다. 구 프로토타입의 미사용 Text 코드까지 전면 수정하지 않았다.

시설 정의의 Passability와 층의 SpecialTileDefinition이 통행 분류 원본이다. ShipModel은 중복 배치 점유와 통행 장애물을 별도로 판정하며 SpecialTileView가 같은 정의를 고체 Collider에 적용한다. 기존 blocked는 잠금/고정 차단 경계, reserved는 통행 가능·배치 금지 경계로 유지한다. 현재7개 고정 특수 타일/2개 시설의 물리 연결을 정적으로 확인했다. 실제 이동/거래/고용/TMP입력·저장 회귀는 미검증이다.


## 현재 Scene·개발 도구 정리 — 2026-10-20

사용자 명시 요청으로 SampleScene/CaptainsCabin/CrewPrototype/PortPrototype와 meta를 삭제하고 Assets/Scenes/Game.unity만 남겼다. Build Settings도 Game 단독이다. 이전 ‘구 Scene 보존/회귀 Scene’ 설명은 당시 이력이며 현재 실행 구성은 단일 Game이다. 구 로직·정의·기본 Prefab·레거시 읽기 호환은 삭제하지 않았다. 빈 Dock/Services·Crossroads/Services만 제거해 실제 GameRoot896객체가 되었다. 계획된 UI Frame 및 참조된 진입/상호작용점은 유지한다.

Assets/Editor/SaveResetWindow.cs는 런타임 포함 없이 개발 슬롯만 관리한다. 메뉴 Great Voyage/Development/Save Data → 현재 ReconstructionStart 또는 선택한 GameStartProfile의 reconstruction- 슬롯을 표시 → Play/컴파일 종료 및 legacySourceCopy 비어 있음 확인 → 사용자 확인 → json/tmp/bak를 SaveResetBackups에 복사·SHA256 대조 → 원본 슬롯 파일 제거. 기존 사용자/Player/프로토타입 저장·ReconstructionImports는 대상으로 삼지 않는다. 실제 초기화/기능 검증은 아직 실행하지 않았다.


## 현재 구조 — S2~S7 재구성 구현, 기능 검증 대기 — 2026-10-20

[ReconstructionPlan 16절](ReconstructionPlan.md#16-s2s7-일괄-재구성-구현-결과--2026-10-20)이 실제 파일·계층·소유권·게이트의 최신 근거다. 이전 절은 각 작업 당시 구현 기록이다. 기존 프로토타입 선택을 새 표준보다 우선하지 않는다.

- 단일 Game Scene→GameRoot→GameComposition/GameRuntime/SimulationLoop/GameFlow. WorldView가 2층+항구5구역·공유 캐릭터 표현을 소유한다. GameState/InventoryState/인물 등록부가 상태의 원본, ShipModel/NavigationRules/CrewBrain/EconomyRules/JournalRules/RestSession이 규칙을 담당한다.
- CrewMovement는 ICrewBody 물리 포트, ReservationService는 선점, SaveCoordinator는 독립 v9 DTO/슬롯, LegacySaveAdapter는 명시 복사본 변환 경계다. 구 SaveService/GameSession 등은 기존 Scene용으로 보존하며 Game에는 GameSession 인스턴스가 없다.
- Editor에서 제작한 UIRoot의 HUD/Context/Screens/Modals/Transitions/SystemModals와 단일 EventSystem을 유지한다. ScreenView/ListItemView 직렬화 키, ScreenStack 정지/포커스, 책임별 Controller로 연결한다. 기본 화면 런타임 생성 없음. 미래 화면 Frame과 미니게임 제외 정책 유지.
- 신규28코드/6Prefab/6SO 및 meta, 기존6코드/23Prefab 변경. 기존 Scene/원본 Data/meta·Packages/ProjectSettings 불변. A/B/C 정적 통과, D/E 실행 미확인. 컴파일 오류/경고0은 기능 통과를 뜻하지 않는다. S1~S7 일괄 기능 검증 및 실제 저장/복원·회귀가 남아 있다.


## 최신 구조 보충 — U0 UI Frame — 2026-10-20

- 최신 사용자 승인으로 미니게임을 제외한 전체 UI Frame을 Editor Prefab으로 제작했다. 상세 자산별 목록·소유권·게이트는 [ReconstructionPlan15절](ReconstructionPlan.md)을 따른다. 아래 S1/과거 프로토타입 기록은 당시 구조다.
- 실제 Game.unity→GameRoot→UIRoot 연결은 그대로다. UIRoot 자체가 Canvas/Scaler/Raycaster를 가지며 HUD, Context, Screens, Modals, Transitions, SystemModals, EventSystem을 소유한다. SystemModals는 전환보다 앞이다. 새 화면은 기본 숨김이고 S1 Journal 진입/닫기 연결은 유지한다.
- UI Prefab 총32개: 기존 UI3개 수정, 신규 화면17/Items11/World UnitStatusBars1. Game Scene/GameRoot/Player/Crew·런타임 C#·저장은 수정하지 않았다. UnitStatusBars는9단계 인물 바인딩용 자산으로 준비했다.
- UIRoot Controller는 아직 S1 일지 입력만 담당한다. 새 화면의 Controller·데이터·pause/포커스·도메인 연결은 계획된 기능 단계에서 구현한다. 화면이 상태·저장을 소유하지 않으며 런타임 전체 화면 생성 코드는 추가하지 않았다.
- 정적 구조·S1 참조·컴파일 확인 완료, 기능 미검증. 이는 미니게임 제외 Frame 구조 수용이며 전체 게임 기능/저장 통합 완료가 아니다.
## 최신 실제 구조 — S1 공통 실행 기반 — 2026-10-20 (Asia/Seoul)

- 사용자 승인 범위는 `codex/game-foundation-reconstruction` 브랜치와 [ReconstructionPlan](ReconstructionPlan.md) S1뿐이다. 전체 설계는 향후 기준안이며 S2 이후·저장 변환·기능 테스트는 미승인이다. HEAD `0e9c96d` 유지, 기존 미커밋 문서 보존.
- 실제 `Assets/Scenes/Game.unity`는 공통 `Assets/Prefabs/Game/GameRoot.prefab` 하나를 포함한다. Systems(GameComposition) / World(StarterShipWorld의 선실) / Characters(기존 Player 중첩) / Cameras / UIRoot로 수명을 분리했다. 신규 UI는 UIRoot·GameHud·JournalScreen의 Editor 제작 Prefab이고 D01/D03 기본 구획·이미지·스크롤·닫기 버튼을 직렬화했다. 전체 Hierarchy·파일별 목록·게이트는 ReconstructionPlan 13절 참조.
- GameComposition이 GameInput/GameFlow를 생성·해제하고 기존 CameraFollow/JournalDesk를 명시적으로 연결한다. GameFlow는 IInteractable 조회/요청, UI 표시, PlayerMovement 입력 차단을 조율한다. UI는 게임 상태·파일 IO를 소유하지 않는다. 새 실행에는 기존 GameSession/항해/선원/경제를 초기화하지 않으며 GameState·시간·저장은 S2에서 연결한다.
- 기존 모터의 AddForce/정규화/가속도 제한·물리 구성, 카메라 추적, Player/JournalDesk의 자산을 재사용했다. PlayerMovement는 기존 Scene용 자체 입력/세션 참조를 보존하고 새 인스턴스에서 공통 입력을 사용한다. 기존 PlayerInteraction은 새 GameRoot 인스턴스에서만 비활성이고 기존 원본 Scene/Prefab은 바꾸지 않았다.
- 5개 신규 Prefab의 정적 구조/참조 검사와 Unity 컴파일 통과. 기능 검증은 미실행이므로 S1 골격 완료·전체 1~7 통합 완료로 기록하지 않는다. 기존 저장 v1~v8/SaveData/SaveService 및 기존 자산 GUID 보존. 기존 독립 프로토타입·PortPanel의 표준 차이는 후속 승인 단계의 전환 대상이다.
- 아래 기록은 당시의 구현/감사 내용으로 보존한다. 새 구조 표준·현재 사용자 승인 범위보다 우선하지 않는다.

## 문서 역할 및 최신 확인 — 2026-10-20 (Asia/Seoul)

- 이 문서는 기존 구현·설계 경위의 기록이다. 신규 구현·재구성은 ArchitectureStandards, 게임 요구는 GameDesign, 절차·권한은 AGENTS/Rules, 실행 단계는 RoadMap을 따른다. 아래의 과거 프로토타입 선택·최종 구조 유보·개발 씬 단독 실행을 신규 표준으로 적용하지 않는다.
- 확인 브랜치는 `codex/journal-autopilot-sleep`, HEAD는 `0e9c96d7b41f836ba0df664c7d0f4805ff35541f` (`roadmap step 7 complete`, 2026-10-19)이다. 7단계 사용자 직접 수정·테스트·커밋 사실은 2026-10-20 사용자 지시로 확인했다. 개별 테스트 결과·범위는 미확인이며 새 구조·통합 게이트는 미확인이다. 이번에는 Unity 실제 구조를 재감사하지 않았다.
- Agents에는 ArchitectureStandards가 추가되어 역할별 문서가 8개다. 아래의 7개 문서·구현 전 main·7단계 미커밋/테스트 대기 등의 표현은 당시 상태다. 현재 상태는 이 요약과 State 및 실제 Git을 함께 확인한다.
- 기존 기능·ID·저장 호환을 보존하며 RoadMap R0 감사 후 R1 목표 구성·전환 계획을 제시한다. 독립 개발 씬, UI 제작 방식, 규칙/표현 책임 결합은 감사 대상이며 문서 기록만으로 실제 자산의 표준 준수 여부를 확정하지 않는다.

## 과거 구현·조사 기록

아래 기존 본문은 당시 이력 그대로 보존한다. 절 제목의 '현재'·'확정'과 향후 작업 표현도 해당 기록 시점의 의미이며, 최신 지침·사용자 승인·현존 사실을 대체하지 않는다.

## 현재 실제 구조

- 루트에 README.md 및 Unity용 .gitignore가 있으며 에이전트 진입점은 AGENTS.md다.
- Agents에는 GameDesign, RoadMap, Architecture, Rules, State, Queue, History의 7개 Markdown 문서가 있다. Agents/References에는 GameDesign에서 참조하는 원본 기획 이미지 26장과 최종기획서.md 원본이 있다. 이 자료는 Unity Assets 밖에서 관리한다.
- 저장소 루트에 Unity 프로젝트의 Assets/, Packages/, ProjectSettings/가 있다. ProjectSettings/ProjectVersion.txt의 Unity 버전은 6000.6.4f1이다.
- Assets/Scenes/SampleScene.unity 및 Assets/Settings/에 2D URP 관련 설정 에셋이 있다. Assets/Welcome/Welcome2DScript.cs는 Unity 템플릿의 기본 스크립트이며, main에는 아직 게임 구현을 포함하지 않는다. 1단계 구현은 codex/player-interaction에서 관리한다.
- Packages/manifest.json에는 Universal Render Pipeline 17.6.0, Input System 1.20.0, Unity Pipeline 0.8.0-exp.1 등이 선언되어 있다. 패키지 선언이 Editor 실행이나 도구 연결의 성공을 의미하지는 않는다.
- Queue와 History 자체가 기록의 원본이다. 별도 JSON 원본·설정·실행 래퍼에 의존하지 않는다. 기록·이관·복구 절차는 Rules에서 관리한다.

## 확정된 관리 설계

- 문서는 역할별 단일 위치에서 관리하고 AGENTS에서 연결한다.
- 작업 시작 시 State와 실제 파일·Git 상태를 확인한다. Queue는 작업 재개·인계 등 실행 맥락이 필요할 때만 관련 부분을 비교하고, History는 더 오래된 근거가 필요할 때 조회한다.
- 지정 구조와 충돌하며 당시 데스크톱에 연결되지 않은 기존 Python 래퍼를 제거했다. 작업 과정은 Queue/History에 기록하고 기기 간 인계는 최신 커밋을 기준으로 한다. 미커밋 기록의 별도 동기화나 실행 차단 도구는 요구사항이 아니다.

## 도구 조사 범위

- 현재 저장소에는 Packages/manifest.json이 있으며 com.unity.pipeline 0.8.0-exp.1이 선언되어 있다. Unity Pipeline·CLI·MCP의 실제 연결·동작 여부는 저장소 파일만으로 확인되지 않는다.
- 2026-10-16 이전 로컬 조사에서는 셸 PATH에서 unity/unity-cli/Unity.exe를 찾지 못했고, C:/Program Files/Unity/Hub/Editor/6000.6.4f1 설치 폴더가 확인되었다. 이는 당시 환경에 관한 기록이며 현재 연결 상태를 보증하지 않는다.
- 이전 로컬 조사에서 `codex mcp list`는 구성된 서버가 없다고 보고했다. 다른 기기나 세션 전체에 서버가 없다는 뜻은 아니다.
- 이전 정적 검토에서 Lit2DSceneTemplate의 dependency GUID 1개와 Renderer2D의 직렬화 참조 7개가 해소되지 않았다. 실제 누락인지 잔여 직렬화 데이터인지는 Editor에서 확인되지 않았다.
- 프로젝트에 연결된 Editor/Pipeline이 확인되면 기존 기능을 우선 재사용한다. 조사만으로 설치·버전 변경·Editor 실행을 수행하지 않는다.

## 현재 연결 및 참조 확인 — 2026-10-18

- 기존 Unity CLI/Pipeline으로 D:/Great Voyage의 Unity 6000.6.4f1 Editor 연결 성공. editor_status는 ready·컴파일 중 아님·Play Mode 정지. MCP 추가 설치 없이 기존 CLI를 사용했다.
- 활성 SampleScene은 수정되지 않은 상태이며 UniversalRP→Renderer2D 참조가 해소된다.
- Editor SerializedObject 조회 결과 Renderer2D probeVolumeResources 6개와 Lit2DSceneTemplate dependency 1개는 실제 객체가 null이고 Entity ID가 남아 있다. m_FallOffLookup은 현재 속성에서 존재하지 않아 잔여 데이터로 판단한다. 참조 수정과 기능 영향 검증은 수행하지 않았다.
- 위 결과가 앞선 도구 조사 당시의 미확인 상태를 갱신한다. 상세 진행 상태는 State를 따른다.

## 작업 브랜치의 1단계 구현

- CaptainsCabin.unity: 선장실 공간·벽·플레이어·책상·카메라·Canvas/EventSystem. 기존 SampleScene 보존.
- Player.prefab: Rigidbody2D/Collider2D와 PlayerMovement/PlayerInteraction, 임시 Head/Body/Hand 외형. 별도 Actor 상속 구조나 Tilemap 없음.
- JournalDesk.prefab: 충돌체와 상호작용 지점. PlayerInteraction이 접근·차폐를 판단해 씬의 JournalPanel을 연다.
- CameraFollow는 플레이어 추적, JournalPanel은 일지 표시·닫기·안내를 담당한다. Input System과 UGUI 사용. 최소 화면의 글꼴은 OS 동적 폰트이며 게임 시간·저장은 다음 단계다.
- Assets/Art/Prototype에 단색 Square/Circle Sprite. Unity 생성 SceneTemplateSettings도 구현 작업에 포함한다.
- 사용자 조작 테스트 통과(2026-10-19). 조작감·객체 계층의 최종 설계는 후속 구체화 대상으로 유보한다.

## 2단계 시간·정지·저장 구현 — 2026-10-19

- GameSession: 하루 600초의 누적 시간과 현재 세션 관리. 새 게임 임시 시작 시각은 1일 08:00. 안정적인 playthroughId/playerId/spaceId 및 물리 위치를 저장·복원한다. 시간 진행은 Time.timeScale 대신 세션의 정지 상태를 확인한다.
- PauseService: 정지 사유마다 IDisposable 핸들을 발급하며 해당 핸들만 해제한다. JournalPanel과 SessionPanel은 서로 독립된 핸들을 소유한다. UI 핸들은 저장하지 않으며 재실행 시 닫힌 UI로 복원한다.
- PlayerMovement/PlayerInteraction: 기존 UI 차단과 세션 정지 상태를 함께 확인한다. 정지 등록 즉시 세션이 플레이어 속도를 0으로 설정한다. 조작감·객체 계층은 재설계하지 않았다.
- SaveData/SaveService: 형식·버전·인물/공간 ID·유한한 위치와 시간 검증. 임시 파일 Flush 후 기존 파일을 File.Replace로 교체한다. 기존 파일이 없을 때 File.Move를 사용한다. 백업은 복구용 파일이며 사용자 선택 저장 슬롯이나 자동 과거 상태 복원 기능은 제공하지 않는다.
- 로드 오류/미지원 버전은 기존 파일을 보존하고 진행을 정지시킨다. 저장 실패 시 오류를 표시하고 메뉴·운영체제의 정상 종료 요청을 중단한다. Editor 정지 버튼·강제 종료는 일반 종료 취소 흐름을 보장할 수 없으므로 별도로 구분한다.
- 저장 위치: Application.persistentDataPath/Saves/Editor/cabin-session.json(Editor) 또는 Saves/Player/cabin-session.json(Player). 개발 중 저장을 실제 플레이 저장과 분리한다. 이번 작업에서는 Play Mode를 실행하거나 저장 파일을 생성하지 않았다.
- SessionPanel: 우측 상단 일수·HH:mm, 일시정지 버튼, 계속/저장 후 종료 메뉴, 오류 표시. 게임 시간의 시작일·시각은 임시 데이터이며 항해 DAY 표시가 아니다.
- CaptainsCabin 씬에 GameSession과 UI를 연결했다. 플레이어 Prefab의 씬 세션 참조는 씬 인스턴스 override로 관리한다. 신규 씬·패키지·입력 자산 변경 없이 기존 1단계 씬을 확장했다.
- 현재는 선장실 하나의 저장 범위다. 공간 전환·계약·재고·이벤트 결과·게임 오버는 해당 시스템 구현 때 확장한다. 별도 GameClock 클래스를 미리 늘리지 않고 현재 시간 책임은 GameSession 안에 한정했다.

## 이벤트 저장의 후속 설계 경계 — 2026-10-19

- 2단계의 일반 위치·시간 저장은 완료했으며 이벤트 체크포인트는 아직 구현되지 않았다. 완전한 물리 결정론은 필요하지 않다.
- 8단계에서 현재 진행 상태와 이벤트 시작 스냅샷을 구분하고, 이벤트 도중 저장 요청이 시작 스냅샷을 선택하도록 저장 정책을 연결한다. 기존 SaveService의 검증·원자 쓰기 기반은 재사용하되 데이터 형식은 확장한다.
- 사건 발생용 난수 진행 정보와 이벤트별 재현 조건을 저장한다. 이벤트 시작·종료 경계와 결과 중복 방지를 함께 설계한다. 구체 요구는 GameDesign의 2026-10-19 보완 절을 따른다.

## 3단계 함선 층·시설 구현 — 2026-10-19

- ShipDefinition(StarterShip.asset)은 층 ID/이름, 배치 격자 원점·크기, 진입점, 고정 차단/예약 칸, 시설 종류와 초기 배치를 정의한다. FacilityDefinition(Bed.asset)은 크기 2×1·허용 층 deck-2·최대 내구도·Bed Prefab을 정의한다. 배치 격자 1칸=1 Unity 단위는 현재 개발용 값이다.
- ShipState는 모델 ID, 활성 층, 시설 인스턴스(ID/종류/층/칸/내구도), 층별 마지막 방문 위치를 저장한다. 정의 자산의 초기 시설은 복사해 런타임 상태와 분리한다.
- ShipLayout은 저장 상태를 검증하고 시설 표현을 생성한다. 현재 층 root만 활성화하고 상태는 세션 데이터에 유지한다. 선원 업무·시뮬레이션은 아직 없으며 향후 비활성 화면 root와 분리해야 한다.
- 고정 차단 칸·예약 칸·시설 점유를 검사하고 계단 진입 칸에서 BFS로 도달 가능한 영역을 구한다. CanPlace는 층 제한·크기·겹침·접근 통로를 검사하며 TryGetAccessPosition은 시설 인접 접근 칸의 지역 좌표를 반환한다. AI 경로 이동이나 배치 편집 UI는 구현하지 않았다.
- 격자의 시설 접근 판정은 칸 단위다. 플레이어 이동은 기존 Rigidbody2D/Collider2D를 유지하며 완전 결정론을 도입하지 않았다. 플레이어 저장 위치는 유한 수치·층 경계로 검사한다. 시설 격자와 연속 물리 위치의 차이를 이유로 정상적인 기존 위치를 강제로 격자 중심에 맞추지 않는다.
- Stairway는 목표 층과 접근 지점·E 안내를 가진다. PlayerInteraction은 책상과 계단 중 접근 가능한 가까운 대상을 선택한다. 계단 이동 시 현재 층 위치를 저장하고 목표 층의 이전 위치 또는 첫 진입점을 사용한다.
- CaptainsCabin 씬의 기존 선장실/책상은 deck-2로 보존하고 deck-1 갑판과 양방향 계단을 추가했다. Bed Prefab은 정의를 바탕으로 Play 시작 시 생성하며 Edit Mode의 씬에 중복 배치하지 않는다. 현재 화면 밖 층의 충돌체는 비활성화한다.
- SaveData v2에 ShipState를 추가했다. v1 파일은 읽기 검증 후 메모리에서 v2로 올리고 초기 함선 상태를 생성한다. 기존 위치·시간은 유지하며 다음 저장 때만 v2를 쓴다. v2에서 함선 상태가 누락되면 오류로 보존한다. 저장 경로는 2단계와 같다.

## 공통 Cell 규격 — 2026-10-19

- 유일한 환산 기준은 ShipDefinition.cellSize다. 현재 1 Cell=1 Unity 단위. 층 원점·첫 진입점·고정 벽/계단/책상의 Transform과 Collider 값은 Cell 단위다. 각 층 root의 scale을 초기화 시 cellSize로 지정하며, 자식의 외형·Collider2D·Trigger Collider2D·상호작용 지점이 함께 환산된다. 별도의 Trigger 행동은 추가하지 않았다.
- 현재 Ship 및 층 root의 원점/회전은 0, 상위 scale은 1을 유지한다. Facilities 컨테이너도 원점/회전 0, scale 1이다. 루트 환산과 자식별 환산을 중복 적용하지 않는다. 층 밖의 별도 Collider는 이 규격을 자동으로 따르지 않으므로 층 root 아래 배치한다.
- FacilityDefinition.size는 정수 점유 칸 수다. Prefab의 루트 scale은 1이고 외형/Collider/Trigger를 1×1 기준 영역에 제작한다. ShipLayout은 시설 인스턴스 scale을 size로 지정한다. Bed Prefab을 이 규약으로 정규화해 실제 침대는 기존과 같은 2×1 Cell 크기를 유지한다. 장식·접근 오프셋 역시 이 정규화 좌표를 따른다.
- 벽 두께·계단 여백은 Cell 비율로 표현한다. 현재 계단 Collider .9×.9는 .9 Cell×.9 Cell이며 논리 점유는 1×1칸이다. 상호작용 검색 거리 1.25는 1.25 Cell로 환산한다. 모든 Collider가 1×1이어야 한다는 규칙은 아니다.
- BFS/점유는 Cell 좌표만 사용하고 시설 접근점/첫 층 진입점은 물리 좌표로 환산한다. 새 게임의 플레이어 배치 좌표도 환산한다. 저장된 플레이어/방문 위치는 기존 Unity 물리 좌표를 유지한다. cellSize는 게임 시작 전 제작 규격이며 플레이 도중 변경하거나 기존 저장의 좌표를 새 크기로 자동 변환하는 기능은 구현하지 않았다.

## 4단계 수동 항해·출입항 — 2026-10-19

- NavigationDefinition(Assets/Data/Navigation/PrototypeSea.asset)은 고정 해역과 항구 A/B의 안정 ID·위치·입항/이탈 반경, 속도·회전 설정을 보관한다. 개발값은 A=(0,0), B=(30,0), 입항 반경3/이탈 반경5, 최대속도4 세계 단위/초다. 선내 Cell 크기와 항해 세계 단위는 독립적이다.
- NavigationState는 세계 위치·방향(0도 동/90도 북)·속도, 현재 항구/출항지/이탈 여부, 승선 여부·승선 복귀 위치, 항해 시작/완료 순번을 보관한다. Unity Transform으로 함선 전체를 움직이지 않는다. 선내 층/플레이어 좌표와 세계 항해 좌표를 분리한다.
- NavigationSession은 저장 상태 검증, 조타 입력, 해역 이동, 자동 입항, 승하선 표시 전환 및 HUD를 담당한다. GameSession이 시간/정지·전체 저장을 소유하고 Awake에서 ShipLayout 다음으로 NavigationSession을 초기화한다. 항구 공간은 Ship 밖에 두며 Cell 크기를 동일하게 적용한다. 비활성화는 화면/충돌체에 한정한다.
- 1층 갑판의 Helm(파란색)과 Gangway(녹색), 항구의 BoardShip(녹색)은 NavigationPoint가 행동을 구분한다. PlayerInteraction은 기존 책상/계단 검색에 이를 추가했다. E로 방향타 사용, A/D 또는 좌우로 회전, W 또는 위로 출항/가속, S 또는 아래로 감속, E/Esc로 조타 종료. 조타 종료 후 속도/방향은 유지한다. 조타 중 선내 이동은 차단하고 메뉴/일지 정지를 항해에도 적용한다.
- 항구는 동일한 개발 공간을 재사용하며 HUD의 실제 항구 ID/이름으로 구분한다. 입항 중에만 승하선 가능하다. 하선 전 선내 위치를 기억하고 승선 시 복원한다. 항구 저장 위치가 선내 층별 방문 위치를 덮어쓰지 않도록 분리한다.
- 출항 시 voyageSequence 증가, 출항지의 이탈 반경 밖으로 나간 뒤에만 해당 항구 재입항 허용. 이동 선분과 입항 원의 최초 교차점을 계산해 긴 프레임에도 항구를 지나치지 않도록 한다. 입항 시 속도0·항구 중심으로 정박, 완료 순번 갱신 및 VoyageCompleted(playthroughId:sequence, portId) 1회 발행. 로드 시 완료 이벤트를 재발행하지 않는다. 계약 차감/정산 소비자는 6단계 이후 연결한다.
- SaveData v3에 navigation을 추가했다. v1/v2의 navigation은 명시적으로 null로 정규화한 뒤 항구 A/승선 상태로 초기화하고 기존 인물·시간·위치·층은 보존한다. v1은 함선도 기존 규칙에 따라 초기화한다. v3는 저장된 항해 ID/항구/수치/순번/승하선 상태를 검증하며 잘못된 데이터를 임의 초기화하지 않는다. 실제 저장 파일은 이번 작업에서 직접 수정하지 않았다.
- 정식 항구 콘텐츠·전체 지도/일지 경로 기록·자동 항해·세계 생성·계약·선원·바람 시스템은 이번 대표 기능 범위에 포함하지 않았다. 현재 항해 위치는 텍스트 HUD로 표시한다. 사용자 직접 기능 테스트 통과 보고를 받았으며 에이전트 추가 테스트는 수행하지 않았다.

## 5단계 선원 업무·생활 — 2026-10-19

- CrewState는 안정 ID·이름·정비 자격·주 업무·업무 가능 상태, 층 ID/연속 Cell 좌표·속도·재탐색 대기, 체력/행복/허기/기력, 행동/대상 ID/최종 목적지/현재 중간 목적지/작업 복귀점/진행 시간을 보관한다. 전체 경로는 런타임 캐시다. CrewRosterState는 목록, 전용 식량·수리도구 수량과 갱신 잔여 시간을 보관한다. Transform이나 화면 활성 여부를 저장 상태의 원본으로 사용하지 않는다.
- CrewSettings는 생성할 선원·표시 Prefab과 조정값이다. StandardCrew는 initialCrew가 비어 있어 새 게임 선원 0명을 유지한다. PrototypeCrew는 동일 Crew Prefab을 사용하는 정비 자격 선원2명·식량2·수리도구8을 생성한다. 개발 구성의 허기/기력 변화는 빠르게 관찰하도록 조정한 임시값이며 최종 밸런스가 아니다.
- CrewSimulation은 GameSession이 정지되지 않았을 때 호출한다. 생활·업무 판단은 기본 .25초 간격, ordinal 선원 ID 순서와 시설 ID 순서로 갱신한다. 이동은 이 간격에서 분리해 FixedUpdate로 수행한다. 잔여 시간도 저장한다. 이는 선원 로직의 재개 일관성을 위한 제한된 구조이며 Unity 물리 전체의 결정론 보장이 아니다.
- ShipLayout의 Cell BFS는 고정 차단 칸과 시설 점유를 피한다. FloorDefinition.connectedFloors는 실제 계단과 같은 연결을 명시하고 각 층 entry를 계단 접근점으로 사용한다. CrewSimulation은 층별 경로를 연결하며 진입점에서만 층을 바꾼다. 플레이어 Rigidbody2D 이동은 그대로 유지한다.
- 업무 후보는 현재 시설 상태에서 조회한다. 정비 자격/수리도구/접근 경로/점유를 검사하고 이동 시작부터 시설을 선점한다. 기능이 남은 손상 시설에 대해 작업 주기마다 도구1개를 소비하고 내구도를 회복한다. 내구도0은 선상 수리 대상에서 제외한다. FacilityDefinition의 repairable/usableAsBed로 기능을 구분한다. 별도 AI용 수리 UI를 열지 않는다.
- 주 업무는 선원 E 상호작용으로 None/Maintenance를 전환한다. None은 작업 금지가 아니라 남은 업무 자동 탐색이다. 현재 대표 업무가 정비 하나뿐이므로 정비 자격이 있는 선원은 주 업무가 없어도 정비를 찾는다. 다른 직업·업무 후보는 아직 추가하지 않았다.
- 식사·취침이 수리보다 우선한다. 식사는 시작 시 식량1개를 소비하고 남은 식사 시간을 저장한다. 야간(기본22~06시) 또는 낮은 기력에는 사용 가능한 침대 접근점으로 이동해 취침하고 주간 기력이 회복되면 기상한다. 취침 중에는 허기/기력 소모를 멈춘다. 자격 있는 야간 항해사의 예외는 해당 선원/업무가 구현되는 단계에서 연결한다.
- 식량 고갈로 허기100이 지속되면 체력과 행복이 감소하며 체력은1 미만으로 떨어지지 않는다. 식량 부족만으로 사망하지 않는다. 전체 능력치/항구 행복 회복/신뢰도·계약·고용 등은 후속 기능 확장 대상이다.
- 선점 사전은 별도 저장하지 않고 행동과 targetId에서 복원한다. 욕구 전환·주 업무 변경·자재 고갈·완료·시설 제거/기능 정지 시 대상/경로/진행시간을 함께 해제한다. SetAvailability는 후속 사망/수감/전투 시스템이 업무를 중단할 연결점이다. 로드 시 유효하지 않은 대상/막힌 경로·중복 선점은 안정 ID 순서로 취소해 정체를 방지하며 손상된 기본 상태는 오류로 보존한다.
- CrewView는 Dynamic Rigidbody2D/Collider2D·보간 표시 및 주 업무 상호작용을 담당한다. FixedUpdate의 목표 Cell 속도를 가속이 제한된 Rigidbody2D.AddForce로 적용하고 실제 위치·속도를 상태에 수집한다. 로드·표시 전환·계단 전환에서만 위치를 복원한다. 화면 밖 층/하선 중에는 표시와 물리 접촉을 끄고 CrewSimulation의 논리 steering으로 진행한다. 선원 간 회피는 혼합 steering에 포함했고 전투 이동은 미구현이다.
- 시설 표현은 원래 색을 보관한 뒤 내구도 비율로 어둡게 표시하며 수리 후 회복한다. 제거된 시설 표현도 정리한다. 고정 시설 편집 UI나 강제 파손 테스트 도구는 추가하지 않았다.
- SaveData v6는 최종 목적지·현재 중간 목적지·작업 복귀점을 저장하고 전체 Nav 경로는 저장에서 제외한다. 연속 위치/속도와 자원/행동/작업 진행도 보관한다. v1~v3 읽기에서 crew를 명시적으로 null로 정규화한 뒤 해당 씬의 설정으로 초기화한다. v4는 기존 자원·행동·경로를 유지하며 속도/재탐색 대기만 0으로 확장한다. 기존 일반 저장은 선원0명으로 확장하고 인물·층·항해·시간을 유지한다. 식량/도구/행동/목적지/진행시간을 함께 저장해 로드 시 초기 자원으로 재충전하지 않는다.
- CaptainsCabin은 기존 cabin-session 슬롯/공간 ID를 유지한다. CrewPrototype.unity는 별도 crew-prototype 슬롯/공간 ID와 PrototypeShip 정의를 사용한다. bed-001 내구도20, bed-002 내구도100으로 시작한다. 두 씬을 동시에 Play하는 구성이 아니며 개발 씬을 단독으로 열어 사용한다. 파일 경로는 Saves/Editor 또는 Saves/Player/<slot>.json이다.
- 컴파일/직렬화 참조 확인 완료, 실제 기능 테스트는 사용자 확인 대기다. 6단계 고용·거래·계약은 구현하지 않았다.

## 선원 Nav + Context steering 보완 — 2026-10-19

- Nav는 기존 Cell BFS와 계단 연결을 재사용한다. 외부 NavMesh 패키지는 도입하지 않았다. CrewSettings.repathInterval(개발 기본3초) 쿨다운이 끝났을 때만 경로를 생성/재탐색한다. 대상 변경·욕구 취소 시에도 최근 탐색 쿨다운은 유지한다. 도달 불가로 대기 중인 작업 탐색도 같은 주기로 제한한다. 중간 목적지 통과는 Nav 재호출이나 쿨다운 초기화 사유가 아니다.
- 경로의 현재 중간 목적지는 이동 방향의 interest 가중치로 입력한다. FixedUpdate에서 기본16방향의 interest와 논리 장애물/예측 선원 위치 및 화면 내 CircleCast의 danger를 결합하고, 이전 속도 가중치·가속 제한으로 이동 속도를 결정한다. 동점에는 진행 방향 기준 우측을 선호해 대칭 회피의 선택을 고정한다. 상위 방향과 인접 방향 점수를 보간한다.
- 중간 목적지는 근접 반경 또는 안전하게 통과한 경우 다음 지점으로 전환한다. 좌표를 칸 중심으로 강제 대입하지 않는다. 최종 시설 접근점은 별도 도착 반경으로 확인한다. 계단의 층 전환은 정의된 진입점에서만 허용한다.
- 화면 안은 Dynamic Rigidbody2D 속도로 실제 충돌을 처리하고 위치/속도를 상태에 수집한다. 화면 밖은 같은 steering과 Cell 기반 원형 여유 공간 검사를 통해 연속 좌표를 적분한다. 비활성 Collider에 의존하지 않는다. 화면 밖에서는 현재 표시 중인 플레이어와의 물리 회피를 수행하지 않는다. 두 경로의 물리 결과가 완전히 동일함을 보장하지 않는다.
- GameSession은 저장 직전에도 실제 선원 위치·속도를 수집한다. 전체 경로 대신 최종·중간 목적지와 진행시간·자원·속도/재탐색 대기를 저장한다. 로드 직후 최초 경로를 복원한 뒤 정상 긴 주기 재탐색을 적용한다. 메뉴 정지는 물리 속도와 재탐색 타이머/생활 진행을 함께 중단한다. HUD의 Nav 횟수는 이번 실행의 경로 생성 호출 수이며 저장 자원/게임 진행 상태가 아니다.
- 조정값은 StandardCrew/PrototypeCrew 자산에 있다. .25초는 생활·업무 판단 간격이며 이동은 FixedUpdate다. 3초/16방향/반경 등은 개발 조정값으로 최종 밸런스 확정이 아니다. 코드 컴파일과 자산 설정은 확인했으며 연속 이동·회피·로드 기능 검증은 사용자 확인 대기다.

## 선원 저장·상호 밀림·작업 복귀·개발 재준비 — 2026-10-19

- CrewState.path는 NonSerialized 런타임 캐시다. 저장에는 destination(최종), nextWaypoint(현재 중간), 현재 층/연속 위치·속도, 행동/대상/진행시간, 작업 시작점, 배회 순번을 포함한다. 목적지 두 개만으로 작업/자원 상태까지 복원할 수 있는 것은 아니다. 로드 시 중간 목적지를 우선 경유하도록 나머지 Nav 경로를 한 번 재구성하며 이후 기존3초 기본 쿨다운을 따른다. 유효하지 않은 중간 목적지는 재사용하지 않는다.
- v4/v5 저장은 호환 DTO로 전체 경로의 첫·마지막 지점만 읽어 새 필드로 옮긴다. 이미 작업 중이던 이전 저장은 현재 위치를 작업 복귀점으로 사용한다. v1~v3의 빈 선원 초기화와 기존 위치·시간·항해 보존 규칙은 유지한다. 실제 파일 변환은 다음 사용자 저장 시 이루어진다.
- 플레이어와 선원은 각각 Dynamic Rigidbody2D와 비 Trigger Collider를 사용한다. 플레이어 이동도 매번 속도를 덮어쓰는 방식에서 가속 제한 AddForce 모터로 보완해 서로의 충돌 밀림을 보존한다. 정지 메뉴/명시적 행동 전환의 즉시 정지는 유지한다. 실제 밀림 강도는 사용자 기능 확인이 필요하다.
- 선원 작업/취침/식사 중 기본 질량4, 평상시1. 작업을 시작한 위치를 별도로 고정 저장하고, 밀려났을 때 최대 .6 Cell/초로 천천히 복귀한다. 작업 반경 .18 Cell 밖에서는 진행시간/회복을 보류한다. HUD는 이때 작업점 복귀로 표시한다. 직접 복귀 가능한 구간은 Context steering으로 돌아오며, 정적 장애물 때문에 우회가 필요하면 같은 긴 주기 Nav 경로로 작업 시작점까지 안내한다. 질량/가속/속도/반경은 CrewSettings에서 조정한다.
- 할 일이 없으면 안전한 가까운 지점을 선택해 배회한다. 배회에도 기존 Nav 캐시+Context steering을 사용하고 탐색 빈도를 제한한다. 배회 순번은 저장되며 고정 계산으로 목적지 후보를 선택한다. 업무·식사·취침이 배회보다 우선한다. 안전한 후보가 없거나 다음 탐색 대기 중에는 휴식한다.
- CrewPrototype에만 선원·침대·자원 재준비 버튼을 추가했다. GameSession의 개발 허용 플래그와 crew-prototype 슬롯을 모두 확인한다. 사용자가 누르면 현재 상태 저장 → 초기 선원/시설/자원과08시 시간 복원 → 다시 저장하며 SaveService의 .bak에는 직전 상태가 남는다. 항해/플레이어 위치는 유지한다. 자동 실행하거나 저장 파일을 삭제하지 않는다. 일반 CaptainsCabin에는 버튼/허용 플래그가 없다.
- 컴파일과 메타데이터 확인 완료. 상호 밀림·작업 복귀·배회·이전 저장 로드 및 버튼 실행은 기능 테스트 미실행/사용자 확인 대기다.

## 6단계 항구 거래·고용·계약 — 2026-10-19

- EconomyState는 금화·직물 수량·항구 재고·명성·계약·마지막 완료 항해 순번·정산 대기·게임 오버를 보관한다. EconomySettings는 대표 상품/후보·항구 가격·임시 밸런스 정의다. EconomySession이 거래와 계약 규칙을 처리하며 PortServicePoint/PortPanel은 진입·표시·사용자 요청만 담당한다. 선원 이동/생활은 기존 CrewSimulation을 재사용한다.
- 대표 일반 무역품은 직물 1종이며 선원 식량으로 소비하지 않는다. 구매는 상인 재고/대금을 함께 이전하고 판매는 대행인에게 한다. 항구 A 구매20/판매15, B 구매28/판매22, 초기 상인 재고 각20은 개발값이다. 일별 가격·수요·재고 갱신, 상품 확장, 협상·평가와 최종 UI는 10단계 범위다.
- 대표 후보 hire-maintainer-001은 정비 특기/주업무로 고용된다. 항해당40, 1~5회 계약은 개발값이다. 선불은 계약 즉시 전액, 후불은 N회 완료 후 전액 채무가 된다. 계약 잔여 횟수가 있으면 승선과 업무를 유지한다. 만료 시 업무 선점과 표시를 해제하고 인물 상태를 계약에 보관한다. 재고용은 동일 인물 ID/능력·욕구를 유지하고 안전한 승선 위치에서 재개한다.
- NavigationSession의 동일 플레이 내 단조 증가 완료 순번을 EconomyState/계약과 함께 저장한다. 이미 반영한 순번은 재처리하지 않는다. 각 만료 계약의 endedPort/endedVoyage는 별도로 고정해 이번 입항의 직전 동행 후보만 재고용 대상으로 삼는다. 만료 채무가 남으면 지급 후 재고용할 수 있다. 선불 반환·항해별 분할 급여는 없다.
- 출항은 GameSession.CanDepart→EconomySession 검사로 연결된다. 기한이 된 후불이 남으면 정산 대기 상태를 저장하고 UI를 연다. 사용자는 수량을 골라 시장 구매가의60%로 매각할 수 있으며 필요한 액수를 넘겨 팔아도 잔여 금화는 보존한다. 확정 시 보유금 지급→남은 미지급액×임시비율1 만큼 명성 감소→채무0→명성0 이하 게임 오버를 한 상태로 저장한다. 실패가 아니면 화면 정지를 해제하고 출항을 요청한다. 취소하면 항구에 남는다.
- 게임 오버는 영속 플래그와 독립 정지 사유로 복원되어 계속 진행할 수 없고 저장 후 종료만 제공한다. 저장 실패 시 기존 GameSession의 오류/정지 경로가 적용되며 추가 거래를 차단한다. 거래/고용/지급/입항·계약 반영/정산 확정은 전체 세션을 한 번 저장한다. UI 미리보기 값은 계약 확정 전 상태를 바꾸지 않는다.
- SaveData v7에 economyEnabled와 economy를 추가했다. 이전 버전은 경제 미사용으로 정규화하고 기존 씬은 경제 없이 유지한다. 새 개발 씬의 경제 상태와 설정을 연결한다. Unity가 null 인라인 객체를 빈 객체로 복원하는 경우를 처리하기 위해 사용 여부를 명시하고, 후보의 선택적 경로도 고용 복사본에서 정규화한다. 저장 크기 1MB 읽기 한도를 쓰기 전에도 적용해 읽지 못할 파일로 교체하지 않는다.
- PortPrototype은 CaptainsCabin의 사본이며 전용 port-prototype 슬롯을 사용한다. PrototypeEconomy와 빈 PortCrew(식량20/도구8), 금화200/명성100/선원0으로 시작한다. 하선 공간의 (-3,1) 직물 상인, (0,1) 판매 대행인, (3,1) 주점에 접근하여 E로 연다. 다른 개발 씬과 동시에 Play하지 않는다. 원본 씬과 기존 저장 파일은 변경하지 않았다.
- 컴파일·필수 참조 확인 완료. 구매/고용/항해/정산/복원/게임 오버의 실제 기능은 사용자 테스트 대기이며 에이전트는 실행하지 않았다.

## 7단계 일지·항해사·취침 — 2026-10-19

- 6단계 커밋 f7c65b3에서 codex/journal-autopilot-sleep으로 분리했다. 기존 PortPrototype을 확장하고 기존 port-prototype 저장 슬롯을 유지한다. 새 개발 씬이나 패키지는 만들지 않았다.
- JournalState는 항해 순번별 출발/도착 항구·계획·확정 경로·실제 사건·메모와 현재 임시 경로/사건·계획 진행·취침 이월 자격을 보관한다. JournalSession이 기록/계획/이월 규칙을 처리한다. 계획점은 세계 좌표이며 선내 Cell 좌표와 독립적이다.
- 일지 접근은 기존 함장실 책상이다. JournalPanel을 D03/E02의 좌측 스크롤 기록·중앙 지도·우측 정보/메모·우상단 닫기 구획으로 교체했다. JournalMap은 고정 가상 항구2개·실시간 배·계획선·확정 경로·사건/메모를 UI 메시로 표시한다. 휠 확대/축소, 오른쪽 드래그 이동, 선택/계획 펜/메모 위치 도구를 제공한다. 과거 항해의 레이어 토글은 표시 설정이며 저장된 항해 자료 자체는 유지된다. 최종 일러스트·장식 자산은 이번 구현에 포함하지 않는다.
- 경로는 .35 세계 단위 간격과 날짜/수면 상태 변화에서 임시 샘플을 추가하고 기록/입항 시 마지막 위치를 포함한다. 기록 버튼이 해당 일자 경로와 이월 가능 경로·사건을 확정 자료에 붙인다. 공백 플래그로 버린 경로를 임의의 직선으로 연결하지 않는다. 날짜별 다중 기록이 가능하다. 완료 항해의 계획·확정 기록은 새 계획 편집으로 변경하지 않는다.
- 기록 이후 일반 상호작용·계획 편집·메모·사건 발생은 이월 자격을 해제한다. 단순 이동·책상 열기/닫기·취침은 자격을 해제하지 않는다. 취침 진입 시 미기록 사건을 소실시키고 마지막 기록 후 다른 상호작용이 없었던 경로만 이월한다. 수면/불침번 이동은 예정 기상 날짜의 기록으로 이월한다. 불침번 종료 후 재취침 시에도 해당 경로는 유지하지만 다시 미기록 사건은 소실한다.
- 계획선은 출항을 실행하지 않는다. 자동 조타 요청과 실제 근무 자격을 별도로 확인한다. CrewSpecialty/PrimaryDuty.Navigation과 navigationExperience>=1을 만족하는 선원이 방향타 접근점(deck-1, 2.5/1.5 Cell)을 선점하고 이동한 뒤 Helming 상태/작업 반경 안에 있어야 조타한다. 선원의 Nav/Context steering·물리·선점·식사 우선 규칙을 재사용하며 조타 업무 중에는 야간 취침 예외를 적용한다. 수동 조타는 자동 조타보다 우선한다. 방향 전환 시 감속하며 계획 종료 시 감속/정지한다.
- 주점은 기존 대표 정비 후보와 additionalCandidates 배열을 같은 계약 규칙으로 처리한다. PortPanel의 다음 후보로 항해사 hire-navigator-001(경력1)을 선택한다. 후보별 계약/재고용 판정을 분리해 이전 정비 계약과 저장을 보존한다. 두 후보의 항해당40은 기존 개발값이다. 자격별 전체 경력·직급 콘텐츠는 후속 확장이다.
- PlayerSleep은 Awake/Sleeping/Watch 상태·침대 ID·06시 기상 시각·기록 날짜·순찰 요구·미충족 결과를 저장한다. 생성된 침대의 BedInteraction을 통해 사용한다. 침대는 CrewSimulation의 같은 선점 표에서 player로 예약하며 불침번 중에도 복귀 침대를 유지한다. 취침은 정지 사유가 아니며 배속 기본20으로 진행하고 플레이어 조작을 막는다. 입항/기상/사건 경계에서 종료된다.
- GameSession이 시간·NavigationSession.AdvanceTime·선원 생활·취침 논리 이동·일지 관찰을 최대 .05초 간격으로 진행한다. 입항 선분 교차 비율만큼 실제 시간을 소모한 뒤 일지 종료/기상→완료 항해 알림/계약·전체 저장 순서로 처리하여 가속 중 항구를 넘거나 정산 전후 상태가 따로 저장되지 않게 한다. 평상시 플레이어/선원의 물리는 FixedUpdate를 유지하고 취침 중 선원은 표시/물리를 끄고 기존 화면 밖 논리 steering으로 같은 경과 시간을 진행한다.
- 8단계 연결점: GameSession.ScheduleEventBoundary/EventBoundaryReached, JournalSession.ObserveEvent, PlayerSleep.BeginWatch. 예약 시각에서 진행 예산을 잘라 사건 처리를 호출하고, 불침번으로 전환하지 않은 수면은 중단한다. BeginWatch는 기상/순찰/침대 복귀/미충족 상태를 지원한다. 실제 사건 발생·이벤트 아이콘 데이터 생성·시작 체크포인트/난수 재현은 8단계에서 구현하며 이번에 사건을 자동 생성하지 않았다.
- SaveData v8의 journalEnabled/journal/sleep가 일지·조타·취침을 저장한다. v7 이하는 새 일지 상태로 확장하고 기존 인물/함선/항해/계약을 보존한다. 이전 저장에 존재하지 않았던 이동 기록은 소급 생성하지 않는다. 복원은 항해→일지→선원 선점→경제→플레이어 위치/취침 순으로 연결해 방향타 선점과 불침번 순찰 시작 위치를 검증한다.
- 컴파일·직렬화 참조 확인만 수행했다. 실제 일지 조작·AI 조타·취침 이동·저장 복원·이월 규칙의 기능 테스트는 사용자 확인 대기다.

### 스크롤바 표시와 Editor 초기화 수정 — 2026-10-20

Editor Enter Play Mode Options의 DisableDomainReload를 해제하여 Domain/Scene Reload를 복원했다. 설치 UGUI Selectable의 정적 초기화와 개체 등록 상태가 분리되는 OnDisable 예외 경로를 피하기 위한 변경이며 패키지 코드는 수정하지 않는다. 실제 연속 Play 재현 검증은 남아 있다.

모든 Scene UI ScrollRect22개(소유 UI Prefab16개)에 AutoHide를 적용하고 초기 빈 Frame의 스크롤바 객체는 숨긴다. ReplaceRows는 강제로 표시하지 않고 빈 목록 숨김/레이아웃 갱신만 요청하며 ScrollRect가 content와 viewport의 실제 bounds로 표시를 결정한다. AutoHideAndExpandViewport로 구도를 바꾸지 않았다. 기능 테스트·저장 변경 없이 정적 검사/컴파일만 확인했다.

## 기능 검증 및 책상 검색 보완 — 2026-10-20

[VerificationReport](VerificationReport.md)에 승인된 실제 Play/대표 통합·저장 경계 결과를 기록했다. 검사 범위에서 기능 미검증 상태를 갱신하며 전체 마우스/기기/장기·실제 이전 저장 수용으로 확대하지 않는다. StarterShipWorld의 JournalDesk InteractionPoint에 검색용 CircleCollider2D Trigger(.2)를 추가했다. 고체 본체와 접근점이 떨어진 구조에서도 InteractionController의 거리/차폐 조회가 대상을 찾게 하는 표현 연결 보완이며, 원본 JournalDesk Prefab·규칙·좌표/저장은 유지한다. 실제 E열기/닫기·입력 복귀와 재정적 검사 통과.
