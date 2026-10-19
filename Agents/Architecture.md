# Architecture

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
