# Architecture

## 현재 실제 구조

- 루트 README.md 및 Unity용 .gitignore가 있으며 에이전트 진입점은 AGENTS.md다.
- Agents에는 GameDesign, RoadMap, Architecture, Rules, State, Queue, History의 7개 Markdown 문서만 둔다.
- Assets/, Packages/, ProjectSettings/는 없다. 게임 코드·씬·에셋·시스템 관계는 구현되지 않았다. 형식상 Unity 폴더를 생성하지 않는다.
- Queue와 History 자체가 기록의 원본이다. 별도 JSON 원본·설정·실행 래퍼에 의존하지 않는다. 기록·이관·복구 절차는 Rules에서 관리한다.

## 확정된 관리 설계

- 문서는 역할별 단일 위치에서 관리하고 AGENTS에서 연결한다.
- 작업 시작 시 State와 실제 파일·Git 상태를 확인한다. Queue는 작업 재개·인계 등 실행 맥락이 필요할 때만 관련 부분을 비교하고, History는 더 오래된 근거가 필요할 때 조회한다.
- 지정 구조와 충돌하며 현재 데스크톱에 연결되지 않은 기존 Python 래퍼를 제거했다. 작업 과정은 Queue/History에 기록하고 기기 간 인계는 최신 커밋을 기준으로 한다. 미커밋 기록의 별도 동기화나 실행 차단 도구는 요구사항이 아니다.

## 도구 조사 범위

- 현재 저장소에 Unity Pipeline·CLI·MCP 구성 또는 패키지 manifest가 없다.
- 로컬 참고 프로젝트의 manifest에는 com.unity.pipeline 0.8.0-exp.1이 있다. 현재 프로젝트에 복사·설치하지 않았다.
- 현재 셸 PATH에서 unity/unity-cli/Unity.exe는 확인되지 않았다. C:/Program Files/Unity/Hub/Editor/6000.6.4f1 설치 폴더는 존재한다. 이는 현재 프로젝트 버전 확정이나 Editor 연결 성공을 뜻하지 않는다.
- 현재 CLI의 codex mcp list는 구성된 서버가 없다고 보고했다. 이 채팅에도 Unity MCP 도구는 노출되지 않았다. 다른 환경 전체에 서버가 없다고 단정하지 않는다.
- 프로젝트에 연결된 Editor/Pipeline이 확인되면 기존 기능을 우선 재사용한다. 조사만으로 설치·버전 변경·Editor 실행을 수행하지 않는다.
