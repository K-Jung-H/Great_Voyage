# 개발 연속성 사용법

Python 3.10 이상, Git, 인증된 Codex CLI가 필요하다. 별도 Python 패키지는 필요 없다.
명령은 저장소 루트에서 실행한다. Windows에서 `python` 대신 설치된 Python 실행파일 절대 경로 또는 `py -3`를 사용할 수 있다.

```powershell
python Agents/continuity.py recover
python Agents/continuity.py session 요청파일.txt
python Agents/continuity.py run -- git status --short
python Agents/continuity.py record assistant.message 응답파일.txt
python Agents/continuity.py resolve 작업ID 확인근거파일.txt
python -m unittest discover -s Agents/tests -v
```

`session`은 매번 문서·State·Queue·실제 Git 상태를 프롬프트에 넣어 새로운 CLI 실행을 시작한다. 같은 CLI 세션 ID에 의존하지 않아 다른 기기에서도 Git 기록으로 인계한다. 프롬프트 원문은 실행 전 기록하고 CLI JSON 이벤트를 출력되는 순서대로 저장한다. stdout/stderr 사이의 절대적인 발생 순서는 OS가 제공하지 않으므로 수신 순서로 기록한다.

`codex exec --json`이 제공하는 대화, 명령, 파일 변경, 도구 이벤트만 수집한다. reasoning 항목은 제외한다. 모든 도구가 사전 이벤트를 제공한다고 보장할 수 없으므로 **명령 실행 전 영속 기록은 `run`을 통할 때 보장**된다. 외부 도구는 AGENTS.md의 planned/result 수동 기록 절차를 따른다. 데스크톱 앱을 평소처럼 실행하는 것만으로 이 래퍼가 연결되지는 않는다.

`events.json`은 원본 이벤트 저장소다. UUID·UTC 시간·종류·작업 ID·원문 payload를 보존한다. 기록은 임시 파일 fsync 후 원자적 교체로 저장한다. Queue는 최근 N개, History는 나머지 이벤트를 같은 원본에서 재생성한다. 따라서 이관 중 중단되더라도 `recover`로 중복/누락 없이 복구한다. N은 `config.json`의 양의 정수 `queue_events`로 설정하고 recover를 실행한다. 이벤트 개수 제한이며 바이트 제한은 아니다. 대규모 기록에서는 원본 전체 재작성 비용이 커질 수 있다.

State의 자동 영역은 시작 후 완료가 없는 명령을 유지한다. 시작 기록이 History로 이동해도 미완료 판정에 포함된다. 종료 코드가 0이 아닌 경우도 결과는 기록되므로 완료 이벤트의 exit_code를 확인한다. 비즈니스 작업 완료 여부와 다음 할 일은 에이전트가 State 수동 영역에 기록한다.

강제 종료 후에는 실제 프로세스, git status/diff, 영향 파일 및 외부 결과를 확인한다. `resolve`에는 확인 근거 파일을 제공하며, 명령을 재실행하는 기능은 없다. 잠금 디렉터리가 남으면 실행 중인 기록기가 없음을 확인한 뒤 `Agents/.continuity-lock`만 제거한다. events.json이 손상되었거나 Git 충돌이 있으면 자동 덮어쓰지 않고 중단한다. Git에서 원본을 복구/병합하고 recover를 실행한다. 기록 수집 오류 시 자식 프로세스를 종료하고 성공으로 표시하지 않는다.

## 기기 변경

작업 종료 시 State 갱신 → git diff 및 기록 검토 → 관련 파일 commit/push. 새 기기에서 clone 또는 깨끗한 작업 트리에서 pull --ff-only → Python/Codex 설치 및 인증 → recover → session 순서다. Git 원격 갱신은 자동 백그라운드 작업이 아니며 각 인계 시 실행한다. 원본 events.json도 반드시 동기화한다. 기기별 Codex 인증 파일은 저장소에 넣지 않는다.

공식 근거: [Codex 비대화형 실행과 JSON 이벤트](https://developers.openai.com/codex/noninteractive). 설치된 CLI의 `codex exec --help`에서 --json, stdin 프롬프트, --sandbox 옵션을 확인했다.
