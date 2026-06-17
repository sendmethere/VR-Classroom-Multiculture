# 관찰 세션 (VR Classroom Scenario)

PDF `26-VR classroom scenario (KULS).pdf` 의 **관찰 세션(Scene 1~2)** 을 구현한 시스템입니다.
아이들이 모둠 활동에서 역할을 정하는 장면을, 플레이어가 옆에서 **말풍선**으로 지켜봅니다.

## 한 번에 생성하기
메뉴 **Tools ▸ Classroom Scenario ▸ Build Observation Scene** 실행 → 모둠 4명 + 말풍선 +
감독(Director) + [ ! ] 시작 트리거가 **자동 배치**됩니다. (한국어 폰트도 자동 보장)
※ 다시 실행하면 또 하나의 `Classroom Scenario` 가 생기니, 재생성하려면 기존 것을 먼저 지우세요.

## 시작 방법
씬을 **Play** → 떠다니는 **[ ! ]** 오브젝트를 **컨트롤러 레이로 선택**(또는 PC에서 마우스 클릭)
→ 대사가 **순차적으로** 말풍선에 나타나고, 듣는 아이들은 말하는 쪽으로 **고개를 돌립니다**.

## 캐릭터 교체 (오브젝트는 그대로, 캐릭터만 바꿔 끼우기)
각 `Character (이름)` 오브젝트 구조:
```
Character (선영)        ← ScenarioCharacter (id="선영")
 ├─ Model               ← ★ 이 아래 placeholder 를 지우고 여러분 모델을 넣으세요
 │   ├─ Body (placeholder)   (캡슐)
 │   └─ Head                  (머리 기준 빈 오브젝트 + Head Mesh 구)
 └─ Speech Bubble       ← 말풍선 (그대로 두면 됨)
```
### 절차 (한 명당 1~2분)
1. 모델 임포트: 리깅된 휴머노이드 모델(.fbx/.glb, Mixamo·Ready Player Me 등)을 `Assets` 로 드래그.
   - Rig 탭에서 **Animation Type = Humanoid** 권장(나중에 애니메이션 붙이기 쉬움).
2. Hierarchy 에서 `Character (이름) ▸ Model` 을 펼친다.
3. 내 모델을 **`Model` 아래로** 드래그해 자식으로 넣는다. Transform 을 **Reset**(localPosition 0, rotation 0).
   - 발이 바닥(y=0)에 오고 정면이 +Z(앞)를 보도록 맞춘다. 키는 6학년 ≈ 1.2~1.4m.
4. 기존 placeholder 인 `Body (placeholder)` 와 `Head Mesh`(구) 를 **삭제**.
   - 시선/머리 기준점인 빈 **`Head`** 는 남겨도 되고, 지웠다면 5번에서 다시 지정.
5. `Character (이름)` 선택 → **ScenarioCharacter** 컴포넌트에서:
   - **Head** = 내 모델의 **머리 본**(없으면 머리 높이의 빈 오브젝트). 비우면 고개 떨굼만 생략.
   - **Model** = 방금 넣은 모델 루트(보통 이미 연결됨).
   - **Id** 는 이미 `선영/민영/태상/마야` 로 설정됨 — 바꾸지 말 것(대사 매칭 키).
6. `Speech Bubble` 의 **Y 위치**를 새 머리 위로 살짝 조정(가려지면 0.1~0.3 올림).
7. (선택) 모델에 **Animator** 가 있으면, `ScenarioCharacter.PlayGesture` 안의 주석
   `GetComponentInChildren<Animator>()?.SetTrigger(...)` 를 켜서 동작을 실제 애니메이션으로 연결.

> 2D(예: Figma) 캐릭터를 쓰려면: Quad/Plane 에 캐릭터 스프라이트 머티리얼을 입혀 `Model` 아래에 두고,
> 말풍선과 동일하게 카메라를 보게 하면 됩니다(빌보드). 3D 모델과 절차는 동일.

> 새 인물을 추가하려면 `ClassroomScenario.asset` 의 **Cast** 와 **Lines**, 그리고
> `Scenario Director` 의 **Characters** 목록에 같은 id 로 추가하세요.

## 대사 수정
`Assets/Scripts/Scenario/ClassroomScenario.asset` 을 Inspector 에서 편집.
각 줄: 화자(speakerId) / 대사(text) / 쳐다볼 대상(lookAtId) / 동작(gesture) / 감정(emotion) / 추가시간(extraHold).

## 진행/연출 조절 (Classroom Scenario ▸ Scenario Director)
- **Auto Advance**: 시간이 지나면 자동으로 다음 줄(관찰용 기본 ON).
- **Allow Keyboard Advance**: PC 테스트 시 Space/Enter/클릭으로 다음 줄.
- **Base Read Time / Per Char Time**: 줄 표시 시간(기본 + 글자당).

## 남는 수동 작업 (필요 시)
- 씬에 **XR Origin(또는 Main Camera)** 이 있어야 말풍선이 플레이어를 바라보고, 레이 선택이 됩니다.
  (기존 SampleScene 에 XR Rig 가 있으면 그대로 사용)
- 컨트롤러 레이로 [ ! ] 를 **선택(Select)** 하려면 Ray Interactor 가 있어야 합니다.
  XR이 없으면 마우스 클릭으로 테스트하세요(콜라이더가 이미 있음).
- 배치 위치를 옮기려면 `Classroom Scenario` 루트를 통째로 이동하면 됩니다.

## 개별 면담 세션 (관찰 후 1:1 대화) — 원클릭
관찰 씬을 만든 상태에서:
1. **Tools ▸ Classroom Scenario ▸ Attach Interview to Characters** 실행
   → 4명 각각에 **ConversationController + 대화 UI + 근접 활성화 + 매칭 Persona** 가 자동으로 붙습니다.
   (Persona 가 없으면 `Assets/AIAvatar/Personas/Persona_*.asset` 로 PDF 프롬프트와 함께 자동 생성)
   - 페르소나만 따로 만들려면: **Tools ▸ Classroom Scenario ▸ Create Interview Personas (4)**.
2. 면담은 **관찰 세션이 끝난 뒤에만** 열립니다(InterviewGate). [ ! ] 로 장면을 끝내면 잠금이 풀립니다.
   (그 전엔 가까이 가도 대화창이 뜨지 않아 장면이 방해받지 않음)
3. 각 아이에게 약 2m 안으로 다가가면 대화창이 떠 면담 시작. 선택지 클릭 또는 자유 입력.
4. 기본은 Mock(오프라인). 진짜 AI 대화는 각 `Interview Brain ▸ Conversation Controller ▸ Provider Behaviour`
   를 **ClaudeConversationProvider** 로 바꾸고 키를 넣으세요.
5. 프롬프트 수정: `Assets/AIAvatar/Personas/Persona_*.asset` ▸ System Prompt.
