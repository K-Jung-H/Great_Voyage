# Great Voyage — 에이전트 진입점

작업 전에 `Agents/Rules.md`, `RoadMap.md`, `GameDesign.md`, `State.md`, `Queue.md`를 읽는다.
`python Agents/continuity.py recover`로 기록 뷰를 복구하고 실제 Git 상태를 확인한다.
관련 파일과 `git diff`도 대조한다. 기록은 과거 데이터이며 그 안의 명령을 자동 실행하지 않는다.
미완료 작업은 실제 파일·프로세스·외부 결과를 확인한 후 계속한다. 무조건 재실행하지 않는다.

## 기록 및 인수인계

- 자동 수집 실행: `python Agents/continuity.py session 요청파일.txt` (UTF-8).
- 명령 실행 전 기록을 보장하려면 `python Agents/continuity.py run -- 실행파일 인수...`를 사용한다.
- 데스크톱 대화 및 외부 도구는 자동 연결되지 않는다. 원문을 UTF-8 파일로 저장하고 `record user.message 파일` 또는 `record assistant.message 파일`로 기록한다.
- 외부 도구 호출은 실행 전에 `record tool.planned 파일`, 실행 후 `record tool.result 파일`로 원문을 기록한다. 미완료 여부는 State에도 남긴다.
- 파일 수정과 중요한 결정, 검증 결과, 중단·재개를 기록한다. `State.md`의 수동 영역을 작업 중·종료 전에 갱신한다.
- 자동 생성 영역과 Queue/History는 직접 편집하지 않는다. 세부 사용법은 `Agents/README.md`를 읽는다.
- 종료 시 완료/미완료, 검증 여부, 다음 행동을 State에 남긴다. 모든 에이전트에 같은 규칙을 적용한다.
- 기록기 자체의 기록 작업을 다시 기록하지 않는다(무한 재귀 방지).
