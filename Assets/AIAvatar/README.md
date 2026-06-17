# AI Avatar — 대화형 아바타 시스템

VR/XR 공간에서 인공지능 아바타와 대화하는 모듈입니다. 지금은 **원통(cylinder) placeholder**
캐릭터이고, 표정·동작·이동을 나중에 확장할 수 있도록 설계되어 있습니다.

## 한 번에 만들기

Unity 메뉴 ▸ **Tools ▸ AI Avatar ▸ Create Conversational Avatar**

생성되는 것:
- `AI Avatar Rig`
  - `Avatar (Cylinder)` — `AvatarCharacter` (감정→색, 시선 추적)
  - `Brain` — `MockConversationProvider`, `ClaudeConversationProvider`,
    `DialogueTreeProvider`, `ConversationController`
  - `Dialogue Canvas` — 월드 공간 UI (이름 / 대사 / 선택지 버튼 / 자유 입력창)
- `Assets/AIAvatar/Personas/SamplePersona.asset` (역할/프롬프트)
- `Assets/AIAvatar/Personas/SampleDialogueTree.asset` (분기 트리 + AI 핸드오프)

> 기본 백엔드는 **Mock(오프라인)** 이라 키 없이 바로 ▶Play 해서 테스트할 수 있습니다.

## 역할/프롬프트 주입

`SamplePersona` 에셋의 **System Prompt** 필드에 상대의 역할/성격을 적습니다.
- `Create ▸ AI Avatar ▸ Character Persona` 로 새 페르소나를 여러 개 만들 수 있습니다.
- 런타임 교체: `ConversationController.SetPersona(persona)`.

## 백엔드(브레인) 바꾸기

`Brain ▸ Conversation Controller ▸ Provider Behaviour` 슬롯에 원하는 컴포넌트를 드래그:

| 컴포넌트 | 동작 |
|---|---|
| `MockConversationProvider` | 오프라인. 키워드 기반 더미 응답 (기본값) |
| `ClaudeConversationProvider` | Anthropic Claude API 실제 대화 |
| `DialogueTreeProvider` | 분기 트리. 노드에서 AI 자유대화로 핸드오프 |

`DialogueTreeProvider`는 `Ai Provider Behaviour` 슬롯에 Mock 또는 Claude를 연결해
핸드오프 대상을 정합니다. (선택지에 없는 자유 입력이 들어오면 자동으로 AI로 전환)

## Claude API 키 설정

`ClaudeConversationProvider`가 키를 찾는 순서:
1. 컴포넌트의 **Api Key** 필드
2. `Assets/StreamingAssets/anthropic_api_key.txt` 파일
3. 환경변수 `ANTHROPIC_API_KEY`

⚠ **보안**: 클라이언트 빌드에 키를 넣으면 추출될 수 있습니다. 배포 시에는
`ClaudeConversationProvider`의 **Proxy Url** 에 자체 서버 주소를 넣으세요.
프록시가 키를 주입해 Anthropic으로 전달하면 키가 단말로 나가지 않습니다.

모델 기본값은 `claude-sonnet-4-6`(대화용). 고품질은 `claude-opus-4-8`,
빠른 응답은 `claude-haiku-4-5-20251001`.

## 근접 활성화 (다가가면 대화창)

`Avatar (Cylinder)`의 **Proximity Activator**가 플레이어(메인 카메라)와의 수평 거리를
재서, `Activate Distance`(기본 2m) 안으로 들어오면 `Dialogue Canvas`를 켜고 대화를
시작합니다. 멀어지면 숨기고 TTS를 멈춥니다.
- `Restart On Return`: 다시 다가올 때 처음부터 재시작할지
- 대화창은 평소 비활성(`Canvas` 꺼짐) 상태이고, `ConversationController.startOnEnable`은 꺼져 있습니다.

`Dialogue Canvas`의 **Dialogue Billboard**가 패널을 항상 플레이어 쪽으로 돌리고, 아바타 주위를
플레이어 방향으로 배치해 캐릭터 뒤로 가려지지 않게 합니다 (`Orbit Distance`, `Height Offset`,
`Follow Position`/`Face Target` 조절).

## TTS (음성 합성)

`Avatar`의 **Rest Text To Speech**(`ITextToSpeech` 구현)가 매 턴 대사를 읽습니다.
기본은 OpenAI Audio Speech 포맷.
- 키: 컴포넌트 `Api Key` → `StreamingAssets/tts_api_key.txt` → 환경변수 `OPENAI_API_KEY` 순.
  키가 없으면 음성만 생략되고 대화는 정상 동작.
- 다른 서비스: `Endpoint` / `Auth Header Name`(예: ElevenLabs `xi-api-key`) / `Auth Prefix`
  / `Body Template`({TEXT}{VOICE}{MODEL}{FORMAT}) 을 바꿔 연결. 배포 시 `Proxy Url` 권장.
- 새 TTS 백엔드는 `ITextToSpeech`를 구현해 끼우면 됩니다 (예: Android 네이티브).

## 입력 수단

- **컨트롤러 레이로 선택지 클릭**: Canvas에 `TrackedDeviceGraphicRaycaster`가 붙어 있습니다.
  XR Origin의 Ray/Near-Far Interactor가 UI를 가리키도록 설정되어 있으면 동작합니다.
  (에디터에서는 마우스로도 클릭됩니다.)
- **자유 텍스트 입력**: 하단 입력창 + 전송 버튼. Enter로도 전송.
- **음성(STT)**: `ISpeechToText` 인터페이스 + `NullSpeechToText` 스텁만 준비됨.
  나중에 실제 STT로 구현하고 `SpeechInputRelay`로 컨트롤러에 연결하세요.

## 확장 지점 (표정 · 동작 · 이동)

매 턴의 응답은 `AvatarTurn`(대사 + 선택지 + `AvatarDirectives`)으로 표준화돼 있습니다.
`AvatarDirectives`는 `emotion / action / gesture / moveTarget / lookAtPlayer`를 담습니다.

새 연출을 추가하려면 **핵심 코드를 건드리지 말고** `AvatarDirectiveHandler`를 상속한
컴포넌트를 아바타에 붙이세요. 매 턴 `Handle(directives)`가 호출됩니다. 예:
- 표정: 블렌드셰이프 핸들러가 `directives.emotion`을 읽어 SkinnedMeshRenderer 가중치 조절
- 동작: Animator 핸들러가 `directives.action`으로 트리거 발사
- 이동: NavMeshAgent 핸들러가 `directives.moveTarget`으로 이동

`AvatarCharacter.SetEmotion / PlayAction / MoveTo`는 지금 placeholder(색/로그)이며,
리깅된 모델로 교체할 때 이 메서드 본문만 채우면 됩니다.

## 한글 폰트

기본 TMP 폰트(LiberationSans)에는 한글 글리프가 없습니다. 셋업 시 Windows의
**Malgun Gothic**으로 동적 TMP 폰트(`Art/Fonts/KoreanDynamic SDF.asset`)를 자동 생성해
에디터에서 한글이 보이도록 합니다.

> **Quest/Android 빌드**에서는 OS 폰트가 없으므로, 한국어 `.ttf`를 프로젝트에 넣고
> **Window ▸ TextMeshPro ▸ Font Asset Creator**로 정적 폰트 에셋을 구워서 교체하세요.

## 그래픽 에셋 (Figma) 가이드

placeholder 원통과 코드로 만든 UI는 **그래픽 없이도 동작**합니다. 아래는 다듬을 때 선택사항.
자세한 폴더/포맷 안내는 `Art/README.md` 참고.
