# Architecture

## 현행 구조·문서 역할 — 2026-10-22

현행 개발 순서는 [RoadMap](RoadMap.md)의 11 최종 그래픽/게임 내용 평가·12 최적화/밸런싱·13 모바일 입력이다. 중복 CompletionPlan은 RoadMap에 통합했고 별도 견인 제안 문서는 아래 확정 결정으로 통합했다. 두 문서의 이전 전문은 [History](History.md#문서-통합-원문-보존--2026-10-22)에 보존했다. 아래 이전 구성·검증 표시는 작성 당시 기록이며 최신 State와 실제 파일을 우선 대조한다.

- 공통 GameRuntime에 EventRules/BattleRules/PrisonerRules·ShipyardRules·CrewLifeRules·MarketRules·PestRules/DetectionRules가 연결돼 있다. 각 상태는 GameState·단일 재고/인물 등록부를 사용한다. 현재 8~10단계 진행 중이며 기능 완료 판정은 아니다.
- SaveCoordinator는 v9 읽기/v10 출력과 기존 원자 저장·백업을 유지한다. 현재 저장 및 사건 체크포인트는 공개 필드 전용 Newtonsoft 직렬화로 선택 상태 null을 보존한다. 사용자 지시로 수정 후 컴파일만 확인했으며 실제 실행 오류 해소는 미확인이다.

### 최근접 항구 견인 — 확정 범위

사용자 ‘최근접 항구로 견인만 적용할 것.’ 승인 범위다. 해상·승선·방향타 기능 정지·전투/사건 종료 조건에서 기존 SessionModals 메뉴의 요청→목적지/비용/계약 처리 확인→재확정으로 입항한다. NavigationDefinition의 보유금25% 내림 비용이며 무일푼도 가능하다. 방향타 파손 시 수동/자동 조타를 중지한다.

NavigationRules가 최근접 목적지·비용·공통 입항을 소유한다. 출항지 이탈 전 같은 항구 복귀는 계약 완료 알림만 생략하고 고유 항해/일지 순번은 유지한다. 그 외는 정상 VoyageCompleted 정산을 사용한다. 확정 전 저장 성공을 확인하고 확정 후 비용/입항/정산을 기존 원자 저장으로 처리한다. 별도 견인 진행 시간을 만들지 않아 진행 중 상태 모델은 없다.

시설·선원·화물 복구·새 부채/명성 벌점·무재료 항구 일감은 추가하지 않았다. 완전 파손 수리는 조선소에서 한다. 항구 수리비 부족 문제는 남아 있고 임의 수익 규칙으로 해결하지 않는다. 비용/중복 처리/복원은 기능 미검증이다. 과거의 항구 일감 포함 권고안은 History의 미채택 제안이며 현행 기획이 아니다.

## 현재 Scene/Prefab 구성

```text
GameRoot (Game.unity 소유)
├── Systems : GameComposition / Battle·Facility·Pest Presentation
├── World (WorldView)
│   ├── Ship/Floors : Deck1_MainDeck, Deck2_CaptainsCabin
│   │   └── 각 층 Geometry / Links / PlacedFacilities
│   ├── Port : Dock / Crossroads / TownDistrict / Market / Tavern
│   │   └── Geometry / Links / 필요한 Services / Entry
│   └── FacilityPreview
├── Characters : Player / EncounterActors / EncounterProjectiles / EncounterPests
├── Cameras : MainCamera / BattleFocus / ShipyardFocus
└── UIRoot (ScreenStack·화면 Controller)
    └── HUD / Context / Screens / Modals / Transitions / SystemModals / EventSystem
```

Systems는 Scene 조립과 직렬화 참조를 담당한다. 정의는 GameStartProfile과 각 ScriptableObject, 상태 원본은 GameState/InventoryState/인물 등록부, 규칙은 역할별 서비스, 표현은 WorldView/각 Presentation 및 Screen Controller다. 이 표는 실제 구성 설명이며 새 규범을 추가하지 않는다. 상세 UI Frame·도안 대응은 ReconstructionPlan6/14절, 신규 설계 기준은 ArchitectureStandards를 따른다.

- WorldView의 구역7개는 함선2층+항구5구역이다. 상태는 표시 구역을 바꾸어도 공통 원본에 남는다. 동적 시설은 각 층 PlacedFacilities, 사건 인물/투사체/유해 생물은 직렬화된 전용 부모에 귀속한다.
- UI 기본 트리는 Editor Scene/Prefab에서 선행 제작한다. TMP/NotoSansKR, ScreenView/ListItemView 참조, ScreenStack의 정지/포커스 수명을 사용한다. 동적 목록은 기존 Item Prefab만 생성한다.
- ContractBoard는 선착장 함선 옆의 Dock/Links 소유다. 함선 콘텐츠 Prefab에는 중복 게시판을 두지 않는다. 기존 행동 Contracts/대상ID contracts를 유지한다. Geometry의 표시는 같은 위치에 대응한다.
- 망원경은 갑판 Links에 귀속하고 정의의 통과 가능한 좌표를 유지한다. 접안 함선·망원경의 표시 크기는 SpriteRenderer.size로 지정한다. 함선 편집의 Place/Remove, Reposition/Upgrade, Unlock은 분리된 세 행이다.
- StarterShipWorld/캐릭터/UI 등 실제 재사용 콘텐츠만 Prefab으로 유지하며 GameRoot 전체를 다시 Prefab화하지 않는다. 구조/데이터 저장 ID를 바꾸거나 실제 슬롯을 초기화하지 않았다.

## 호환·개발 도구·검증 경계

- Passability 정의와 SpecialTileView를 기준으로 점유/통행을 구분한다. ShipModel은 배치·접근성·층·시설 상태를 소유하고 표현 객체의 표시를 상태 원본으로 사용하지 않는다.
- Game의 현재 실행은 GameRuntime/SaveCoordinator를 사용한다. 구 GameSession/SaveService/프로토타입 코드와 원본 정의는 이전 저장 변환·비교 근거로 남아 있으며 현행 신규 표준보다 우선하지 않는다. 사용하지 않는 Scene4개는 이전 승인으로 제거했고 Build Settings는 Game 단독이다.
- Editor 전용 SaveResetWindow는 사용자 확인 후 reconstruction 슬롯 파일만 백업/해시 확인·초기화한다. 제품 UI·일반 실행의 자동 복구 수단이 아니다. 이번 문서/Scene 수정에서 실행하지 않았다.
- 1~7의 이전 대표 통합 검증은 VerificationReport에 한정한다. 8~10 및 이번 변경은 정적 검사/컴파일과 기능 검증을 구별한다. 최종 수치는 State/Queue를 따르며 실제 조작/저장 복원을 통과했다고 주장하지 않는다.
- 기존 현황 전문과 폐기 제안은 History에, 재구성 설계/도안 대응은 ReconstructionPlan에 남긴다. 원본 기획·이미지·확정 게임 규칙은 문서 정리로 변경하지 않는다.
