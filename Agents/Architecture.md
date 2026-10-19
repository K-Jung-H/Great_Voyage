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
