# VR Classroom Multiculture

초등학교 모둠 활동에서 발생하는 **다문화 배경 학생에 대한 언어적·정서적 배제** 상황을 VR로 재현하고,
학습자가 이를 **관찰**한 뒤 등장인물들과 **1:1로 면담**하며 성찰하도록 설계한 VR 시뮬레이션입니다.

> 과목: VR 콘텐츠 개발 · 주제: 다문화 차별 상황 체험·성찰 시뮬레이션
> 엔진: Unity 6 (URP 17.4) · XR Interaction Toolkit 3.4.1 / OpenXR · TextMeshPro

근거 문헌: 이수민·양난미·이아라 (2021), *다문화 청소년이 학교에서 경험하는 차별에 대한 현상학적 연구*,
한국콘텐츠학회논문지, 21(5), 776–793.

---

## 데모 영상

[![VR Classroom Multiculture 데모 영상](https://img.youtube.com/vi/8xSW63mGBc0/maxresdefault.jpg)](https://youtu.be/8xSW63mGBc0)

> 썸네일을 클릭하면 YouTube에서 데모 영상을 볼 수 있습니다.

---

## 콘텐츠 구성

콘텐츠는 두 개의 세션으로 이어집니다.

| 세션 | 내용 | 상호작용 |
|------|------|----------|
| **① 관찰 세션** | 4명(태상·선영·민영·마야)이 역할을 정하는 과정에서 마야가 배제되는 장면을 **말풍선 대화**로 순차 재생 | 떠다니는 `[ ! ]` 오브젝트와 상호작용해 시작, 이후 관람 |
| **② 개별 면담 세션** | 관찰이 끝난 뒤 각 인물에게 다가가 **AI 기반 1:1 대화** | 근접(약 1m) 시 대화창 활성화, 선택지·자유 입력 |

**학습 의도** — 학습자가 차별 장면을 3인칭으로 관찰한 뒤, 가해·피해·중재 인물과 직접 대화하며 각자의
입장과 감정을 확인하고 자신의 행동을 돌아보도록 유도합니다.

---

## 설계 원칙

- **데이터 / 로직 / 표현 분리** — 대사와 인물은 `ScriptableObject`(ScenarioScript, CharacterPersona)에 담고, 재생 로직(Director)과 표현(SpeechBubble, DialogueUI)은 데이터를 모른 채 동작합니다.
- **교체 가능(swappable) 구조** — 각 캐릭터는 `Model` 슬롯만 비워 두어 모델만 끼우면 되고, 대화 백엔드는 `IConversationProvider` 인터페이스로 주입식 교체(Mock ↔ Claude)됩니다.
- **에디터 자동화** — 배치·와이어링·에셋 생성을 모두 에디터 스크립트가 수행해 메뉴 클릭만으로 전체 씬이 자동 구성됩니다.
- **확장 훅** — 동작은 `ScenarioCharacter.PlayGesture` / `AvatarDirectiveHandler` 훅을 통해 Animator·블렌드셰이프로 무손상 확장이 가능합니다.

---

## 주요 기능

### 관찰 세션
- 대본(PDF Scene 2)을 동작·감정과 함께 작성한 **17개 대사**를 위에서 아래로 순차 재생 (`ScenarioDirector`).
- 화자는 대상을 향해 몸을 돌리고, 나머지 인물은 화자 쪽으로 고개를 돌리는 **시선 연출**.
- 귓속말 · 혼잣말 · 돌아보기 · 기죽음 · 말 더듬음 · 고개 떨굼 등 **연기 동작 → 말풍선 스타일** 매핑.
- 머리 위 **월드 공간 말풍선**: 타자기 효과, 페이드, 카메라를 향하는 빌보드 처리.

### 면담 세션 (AIAvatar 재사용)
- **`IConversationProvider`** 추상화로 대화 백엔드 교체
  - `MockConversationProvider` — 오프라인(키 불필요) 기본값
  - `ClaudeConversationProvider` — Anthropic Claude Messages API 연동, 페르소나 시스템 프롬프트 기반 실시간 응답(JSON: 대사·선택지·표정)
- **`CharacterPersona`** — 인물별 시스템 프롬프트(선영=불만·차별, 민영=조롱·편견, 태상=중재자, 마야=위축·당사자)와 공통 상황 맥락·면담 규칙 포함.
- **`InterviewGate`** — 관찰이 끝나기 전에는 모든 근접 활성화를 잠가, 대화창이 장면 중간에 뜨는 문제를 방지.
- 한국어 렌더링은 `AIAvatarFontUtil`이 OS 한글 폰트로 동적 TMP 폰트를 생성해 전역 Fallback에 등록.

---

## 에디터 자동화 (수동 작업 최소화)

| 메뉴 | 동작 |
|------|------|
| **Build Observation Scene** | 모둠 4명을 책상 둘레에 자동 배치, Director·`[ ! ]` 트리거 생성·연결, 대본 에셋·한국어 폰트 보장 |
| **Create Interview Personas (4)** | 인물 프롬프트가 채워진 `CharacterPersona` 4종 생성 |
| **Attach Interview to Characters** | 4명에 ConversationController + 대화 UI + 근접 활성화 + 페르소나를 자동 부착, InterviewGate 연결 |
| **Interview Provider ▸ Use Claude / Use Mock** | 면담 대화 백엔드를 일괄 전환 |

---

## 사용자 흐름

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

---

## 기술 스택

- **엔진/렌더** : Unity 6, Universal Render Pipeline 17.4
- **XR** : XR Interaction Toolkit 3.4.1, OpenXR (XRSimpleInteractable, TrackedDeviceGraphicRaycaster, XRUIInputModule)
- **UI/텍스트** : World-space Canvas, TextMeshPro (동적 한글 폰트)
- **AI 대화** : Anthropic Claude Messages API (provider 패턴으로 주입)
- **설계 패턴** : ScriptableObject 데이터 주도, 인터페이스 기반 의존성 주입, 컴포넌트 합성, 에디터 툴링, 이벤트 기반 세션 전환

---

## 프로젝트 구조

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

> 상세 구현 보고서는 [`Assets/Scripts/Scenario/report.md`](Assets/Scripts/Scenario/report.md) 참고.

---

## 한계 및 향후 과제
- 동작(고개 떨굼·기죽음 등)은 현재 단순 본 회전이며, 전용 애니메이션 클립/블렌드셰이프로 고도화 가능.
- 음성(TTS)·음성 입력(STT)은 인터페이스만 마련되어 있어 실제 키 연결 시 음성 면담으로 확장 가능.
- 면담 평가/로그(학습자 응답 기록·성찰 리포트)는 미구현 — 교육 효과 측정을 위한 향후 과제.
