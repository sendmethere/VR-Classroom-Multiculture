# VR 다문화 교실 시나리오 구현 보고서

> 과목: VR 콘텐츠 개발 · 주제: 다문화 차별 상황 체험·성찰 시뮬레이션
> 엔진: Unity 6 (URP 17.4) · XR Interaction Toolkit 3.4.1 / OpenXR · TextMeshPro

---

## 1. 개요

본 프로젝트는 초등학교 모둠 활동에서 발생하는 **다문화 배경 학생에 대한 언어적·정서적 배제** 상황을
VR로 재현하고, 학습자가 이를 **관찰**한 뒤 등장인물들과 **1:1로 면담**하며 성찰하도록 설계한
시뮬레이션이다. 시나리오는 제공된 대본 `26-VR classroom scenario (KULS).pdf` 와 그 근거 문헌
(이수민·양난미·이아라, 2021, *다문화 청소년이 학교에서 경험하는 차별에 대한 현상학적 연구*,
한국콘텐츠학회논문지, 21(5), 776–793)을 바탕으로 한다.

콘텐츠는 두 개의 세션으로 구성된다.

| 세션 | 내용 | 상호작용 |
|------|------|----------|
| **① 관찰 세션** | 4명(태상·선영·민영·마야)이 역할을 정하는 과정에서 마야가 배제되는 장면을 **말풍선 대화**로 순차 재생 | `[ ! ]` 오브젝트와 상호작용해 시작, 이후 관람 |
| **② 개별 면담 세션** | 관찰이 끝난 뒤 각 인물에게 다가가 **AI 기반 1:1 대화** | 근접(약 1m) 시 대화창 활성화, 선택지·자유 입력 |

설계 목표는 **"오브젝트는 그대로 두고 캐릭터 모델만 교체"** 할 수 있는 데이터 주도·교체 가능
구조와, **에디터 메뉴 클릭만으로 전체 씬이 자동 구성**되는 자동화였다.

---

## 2. 시스템 아키텍처

```
                         ┌──────────────────────────────────────────┐
                         │            Editor 자동화 (1-click)          │
                         │  ScenarioSceneSetup / KulsPersonaSetup /   │
                         │  InterviewSetup                            │
                         └───────────────┬────────────────────────────┘
                                         │ 생성·배치·연결
                 ┌───────────────────────┼────────────────────────────┐
                 ▼                       ▼                            ▼
     ┌────────────────────┐   ┌────────────────────┐    ┌────────────────────────┐
     │   관찰 세션 (런타임)   │   │   데이터(에셋)        │    │   면담 세션 (AIAvatar)    │
     │ ScenarioTrigger[!]  │   │ ScenarioScript      │    │ ConversationController  │
     │ ScenarioDirector    │◄──│ (Cast + 17 Lines)   │    │  ├ Mock / Claude 제공자  │
     │ ScenarioCharacter×4 │   │ CharacterPersona×4  │───►│ DialogueUI + Billboard  │
     │ SpeechBubble×4      │   └────────────────────┘    │ ProximityActivator      │
     └─────────┬──────────┘                              └───────────┬─────────────┘
               │ onFinished                                          │ 잠금 해제
               └──────────────────► InterviewGate ───────────────────┘
```

핵심 설계 원칙:

- **데이터 / 로직 / 표현 분리** — 대사와 인물은 `ScriptableObject`(ScenarioScript, CharacterPersona)에
  담고, 재생 로직(Director)과 표현(SpeechBubble, DialogueUI)은 데이터를 모른 채 동작한다.
- **교체 가능(swappable) 구조** — 각 캐릭터는 `Model` 슬롯만 비워 두어 모델만 끼우면 된다.
  대화 백엔드는 `IConversationProvider` 인터페이스로 주입식 교체(Mock ↔ Claude)된다.
- **확장 훅** — 동작은 `ScenarioCharacter.PlayGesture` / `AvatarDirectiveHandler` 훅을 통해
  Animator·블렌드셰이프로 무손상 확장 가능.
- **에디터 자동화** — 배치·와이어링·에셋 생성을 모두 에디터 스크립트가 수행해 수동 작업을 최소화.

---

## 3. 관찰 세션 구현

### 3.1 데이터 모델 (`ScenarioModels.cs`)
- `ScenarioLine` : 한 대사 줄. `speakerId`(화자), `text`(대사), `lookAtId`(쳐다볼 대상),
  `gesture`(연기 동작), `emotion`(감정), `extraHold`(추가 표시 시간).
- `Gesture` : `Whisper`(귓속말) · `Thought`(혼잣말) · `LookAtTarget`(돌아보기) · `HeadDown`(고개 떨굼)
  · `Shrug`(기죽음) · `Stammer`(말 더듬음) 등. 동작 → 말풍선 스타일(`BubbleStyle`)로 매핑.
- `ScenarioScript`(ScriptableObject) : `cast`(등장인물 명단+색) + `lines`(순차 대사 17줄).
  대본 PDF의 Scene 2 전체를 동작·감정과 함께 미리 작성해 에셋으로 저장.

### 3.2 순차 재생 (`ScenarioDirector.cs`)
코루틴으로 대사를 위에서 아래로 한 줄씩 처리한다. 매 줄마다:

1. `speakerId` 로 화자 캐릭터를 찾는다.
2. **화자**는 `lookAtId` 대상(없으면 모둠 중앙)을 향해 몸을 돌린다.
3. **나머지 인물**은 화자 쪽으로 고개를 돌린다(돌아보기). 단, 귓속말/혼잣말은 시선을 고정하지 않는다.
4. 이전 말풍선을 숨기고 현재 말풍선을 띄운다(동시 표시 1개로 가독성 확보).
5. 글자 수 기반 시간(`baseReadTime + 글자수 × perCharTime + extraHold`)만큼 대기 후 다음 줄로.
   - `autoAdvance`(자동) 외에, PC 테스트용으로 Space/Enter/클릭(`Advance`) 즉시 진행도 지원.

종료 시 `onFinished` 이벤트를 발생시켜 면담 세션 잠금을 해제한다.

### 3.3 말풍선 (`SpeechBubble.cs`)
- 캐릭터 머리 위 **월드 공간 Canvas**. 이름 + 대사를 **타자기 효과**로 출력하고 `CanvasGroup`으로
  부드럽게 페이드한다.
- `LateUpdate`에서 항상 카메라(플레이어)를 향하도록 회전(빌보드). 위치는 부모(캐릭터) 기준 고정이라
  캐릭터가 회전해도 머리 위를 유지한다.
- `Whisper`/`Thought` 스타일은 이름 접미사("(귓속말)"/"(속마음)")·기울임·배경색으로 구분.

### 3.4 캐릭터 슬롯 (`ScenarioCharacter.cs`)
- `Model`(교체 슬롯) · `Head`(말풍선 기준·고개 회전) · `Bubble` 참조를 가진 얇은 래퍼.
- 외부 공개 API는 `Say` · `LookAt` · `ResetLook` · `PlayGesture` 로 최소화.
- 시선은 **루트 yaw 회전**으로 구현해 모델 교체·Animator와 충돌하지 않는다. 고개 떨굼은 `Head` 본의
  피치 회전으로 표현(추후 전용 애니메이션으로 대체 가능).

### 3.5 시작 트리거 (`ScenarioTrigger.cs`)
- 떠다니며 회전하는 **`[ ! ]`** 오브젝트. `XRSimpleInteractable.selectEntered`(컨트롤러 레이 선택)에
  자동 구독하여 `Director.Play()` 호출. **비-VR 환경 테스트를 위해 `OnMouseDown`(마우스 클릭)도 지원.**
- 발동 후 자기 자신을 숨겨 1회성 시작을 보장.

---

## 4. 개별 면담 세션 구현 (AIAvatar 재사용)

기존 대화 프레임워크(`AIAvatar`)를 재사용하여, 관찰 씬의 **같은 캐릭터**에 대화 기능을 부착한다.

- **`IConversationProvider`** 추상화로 백엔드 교체:
  - `MockConversationProvider` — 오프라인(키 불필요) 기본값.
  - `ClaudeConversationProvider` — Anthropic Claude Messages API 연동. 페르소나 시스템 프롬프트를
    인물 정체성으로 사용하고, 응답을 JSON(대사·선택지·표정)으로 받아 파싱. 키는
    *인스펙터 → StreamingAssets 파일 → 환경변수* 순으로 해석(빌드 배포 시 프록시 권장).
- **`CharacterPersona`**(ScriptableObject) — 인물별 시스템 프롬프트. PDF의 인물 프롬프트
  (선영=불만·차별, 민영=조롱·편견, 태상=중재자, 마야=위축·다문화 당사자)와 **공통 상황 맥락**,
  **면담 규칙**(한국어·6학년 말투·캐릭터 이탈 금지 등)을 담아 4종 자동 생성.
- **표현·상호작용** : 월드 `DialogueUI`(이름·대사·선택지 버튼·자유 입력), `DialogueBillboard`(플레이어
  쪽 배치·정면 유지), `ProximityActivator`(근접 시 대화 시작), `RestTextToSpeech`(선택적 음성).
- **`InterviewGate`** : 관찰 세션이 끝나기 전에는 모든 근접 활성화를 꺼 둬, 플레이어가 `[ ! ]`로
  다가갈 때 인접한 학생의 대화창이 장면 중간에 뜨는 문제를 방지. `Director.onFinished` →
  `Gate.Unlock` 으로 연결.

한국어 렌더링은 `AIAvatarFontUtil`이 OS 한글 폰트로 동적 TMP 폰트를 생성해 전역 Fallback에 등록한다.

---

## 5. 에디터 자동화 (수동 작업 최소화)

| 메뉴 | 동작 |
|------|------|
| **Build Observation Scene** | 모둠 4명을 책상 둘레(반경 ≈ 0.95m)에 자동 배치(placeholder 캡슐+머리+말풍선), Director·`[ ! ]` 트리거 생성·연결, 대본 에셋·한국어 폰트 보장, README 생성 |
| **Create Interview Personas (4)** | PDF 인물 프롬프트가 채워진 `CharacterPersona` 4종 생성 |
| **Attach Interview to Characters** | 4명 각각에 ConversationController + 대화 UI + 근접 활성화 + 매칭 페르소나를 자동 부착, InterviewGate 연결 |
| **Interview Provider ▸ Use Claude / Use Mock** | 면담 대화 백엔드를 일괄 전환 |

직렬화 필드 연결은 `SerializedObject`/전용 `EditorWire` 메서드로 처리하며, 검증된 UI 빌더
(`AIAvatarSetup.BuildDialogueUI`)를 재사용해 중복을 줄였다(접근 수준만 `internal`로 공개).

---

## 6. 사용자 흐름 (User Flow)

```
[준비/저작]  Build Observation Scene → (캐릭터 모델 교체) → Create Personas
             → Attach Interview → (선택) Use Claude + API Key
                                   │
[체험 시작]  ▶ Play ─ 플레이어가 교실에서 떠다니는 [ ! ] 로 접근
                                   │  컨트롤러 레이로 선택(또는 마우스 클릭)
[관찰 세션]  ─ 17개 대사가 말풍선으로 순차 출력
             ─ 귓속말·혼잣말·돌아보기·고개 떨굼 등 연기 동시 재생
                                   │  마지막 대사 후 onFinished
[전환]       ─ InterviewGate 잠금 해제 (이제 면담 가능)
                                   │
[면담 세션]  ─ 각 학생에게 다가가면 대화창 활성화
             ─ 선택지 클릭 또는 자유 입력으로 1:1 성찰 대화
             ─ (Claude 연결 시) 인물 프롬프트에 따른 실시간 응답
```

체험의 학습 의도: 학습자가 차별 장면을 **3인칭으로 관찰**한 뒤, 가해·피해·중재 인물과 **직접 대화**하며
각자의 입장과 감정을 확인하고, 자신의 행동을 돌아보도록 유도한다.

---

## 7. 구현 과정의 주요 의사결정·이슈 해결

- **시작 조건** : "느낌표 오브젝트와 상호작용 시 시작" 요구를 XR 선택 이벤트로 구현하되, 헤드셋이 없는
  개발 환경을 위해 마우스 클릭 폴백을 병행.
- **재생이 "멈춤"** : 재생 시 에디터가 멈추는 현상은 코드 무한 루프가 아니라 **OpenXR가 헤드셋을
  기다리며 초기화**하는 문제로 진단. PC 테스트 시 OpenXR(Desktop) 해제 또는 시뮬레이터 사용으로 해결.
- **T-pose 문제** : 교체한 모델이 T자세로 보이는 것은 애니메이션 미재생 때문 → `idle`/`sit` 애니메이터
  컨트롤러 연결로 해결. 시선(루트 회전)은 Animator와 충돌하지 않도록 설계.
- **말풍선 겹침** : 모델 키 변화로 말풍선이 캐릭터와 겹쳐 → 말풍선 높이(RectTransform Pos Y) 조절 안내.
- **대화창 중복** : 좁은 모둠에서 근접 시 여러 대화창이 동시에 뜨는 문제 → 근접 거리 축소,
  그리고 InterviewGate로 관찰 종료 전 차단.
- **"(목업) 응답"** : 키만 입력하고 Provider가 Mock이면 가짜 응답 → 일괄 전환 메뉴로 Claude로 교체.

---

## 8. 기술 스택 요약
- **엔진/렌더** : Unity 6, Universal Render Pipeline 17.4
- **XR** : XR Interaction Toolkit 3.4.1, OpenXR (XRSimpleInteractable, TrackedDeviceGraphicRaycaster, XRUIInputModule)
- **UI/텍스트** : World-space Canvas, TextMeshPro(동적 한글 폰트)
- **AI 대화** : Anthropic Claude Messages API (provider 패턴으로 주입)
- **설계 패턴** : ScriptableObject 데이터 주도, 인터페이스 기반 의존성 주입, 컴포넌트 합성,
  에디터 툴링, 이벤트 기반 세션 전환

---

## 9. 한계 및 향후 과제
- 동작(고개 떨굼·기죽음 등)은 현재 단순 본 회전이며, 전용 애니메이션 클립/블렌드셰이프로 고도화 가능.
- 음성(TTS)·음성 입력(STT)은 인터페이스만 마련되어 있어 실제 키 연결 시 음성 면담으로 확장 가능.
- 면담 평가/로그(학습자 응답 기록·성찰 리포트)는 미구현 — 교육 효과 측정을 위한 향후 과제.
- 좁은 모둠에서의 단일 대화 보장은 거리 기반 + 게이트로 처리했으나, "최근접 1인만" 코디네이터로
  더 견고히 할 수 있음.

---

## 부록 A. 파일 구성

```
Assets/Scripts/Scenario/
 ├ ScenarioModels.cs        # 데이터(Line/Cast/Script ScriptableObject)
 ├ SpeechBubble.cs          # 월드 말풍선(타자기·페이드·빌보드)
 ├ ScenarioCharacter.cs     # 교체 가능한 캐릭터 슬롯 + 시선/동작
 ├ ScenarioDirector.cs      # 순차 재생 감독
 ├ ScenarioTrigger.cs       # [ ! ] 시작 트리거
 ├ InterviewGate.cs         # 관찰 종료 후 면담 잠금 해제
 └ Editor/
    ├ ScenarioSceneSetup.cs # 관찰 씬 1-click 생성 + 대본 에셋
    ├ KulsPersonaSetup.cs   # 인물 페르소나 4종 생성
    └ InterviewSetup.cs     # 면담 자동 부착 + 프로바이더 전환

Assets/AIAvatar/            # 재사용한 대화 프레임워크(Controller/Provider/UI/TTS …)
Assets/AIAvatar/Personas/   # Persona_태상·선영·민영·마야 (자동 생성)
```

## 부록 B. 관찰 세션 대사 구조 (요약)
17개 대사로 구성. 주요 연기 매핑:

| # | 화자 | 동작 | 비고 |
|---|------|------|------|
| 1–2 | 선영·민영 | 귓속말 | 마야 배제 발화 |
| 3–7 | 태상·민영·선영 | 일반/돌아보기 | 역할 분담 |
| 8 | 마야 | 기죽음 | 침묵("……") |
| 10 | 마야 | 혼잣말 | 속마음 말풍선 |
| 11–12 | 선영·민영 | 돌아보기 | 압박·비아냥 |
| 14 | 마야 | 말 더듬음 | "나… 나는…" |
| 15 | 선영 | 돌아보기(화남) | "제대로 말을 해!" |
| 16 | 마야 | 고개 떨굼 | 정서적 배제 절정 |
| 17 | 태상 | (밝음) | 중재·포용 |
