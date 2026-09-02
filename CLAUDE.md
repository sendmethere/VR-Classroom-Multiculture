# CLAUDE.md — 이 프로젝트에서 Claude 가 지켜야 할 작업 방식

## 🛠️ Tools 메뉴를 추가/변경했을 때 (가장 중요)
에디터 `Tools ▸ …` 메뉴 항목을 **새로 추가하거나 바꿨다면**, 응답에서 반드시 **이모지와 함께 굵게 강조**해
사용자가 곧바로 실행할 수 있게 안내한다. 예시:

> 🛠️✅ **Unity 상단 메뉴에서 `Tools ▸ Classroom Scenario ▸ Add Voice Input (Push-To-Talk)` 를 실행하세요.**

규칙:
- 실행이 필요한 메뉴가 여러 개면 **순서대로 번호**를 매겨 나열한다.
- 각 메뉴가 하는 일을 한 줄로 덧붙인다.
- "코드만 고치고 끝"내지 말 것 — 에디터 메뉴 실행이나 씬 재생성이 필요한 변경은 **항상 이렇게 눈에 띄게** 안내한다.
- 사용자가 빠르게 조치할 수 있게 하는 것이 목적이다.

## 프로젝트 세팅 규칙
- **API 키**: `Assets/StreamingAssets/anthropic_api_key.txt`(Claude 대화), `openai_api_key.txt`(STT+TTS 공용).
  키 파일과 `*.md` / `*.docx` 는 `.gitignore` 로 **커밋 금지**.
- **에디터 도구 중심**: 씬 구성·페르소나·대화 리그는 `Tools ▸ Classroom Scenario ▸ *` 메뉴로 만든다.
  코드에서 페르소나/프롬프트를 바꾸면 **Create Interview Personas (4)** 를 재실행해야 자산에 반영된다.
- **이미 만들어진 씬 보정**: `Attach Interview to Characters` 는 **새 캐릭터에만** UI를 만든다.
  기존 씬에는 보정 메뉴(Add Voice Input / Add '말하기' Button / Add Observation Skip Button)를 사용·안내한다.

## 음성/대화 흐름 메모
- 음성 입력: 대화 중 **V 키** 또는 **● 말하기 버튼**을 누르는 동안 녹음 → 떼면 Whisper(`whisper-1`)로 인식.
- 인식 결과는 **바로 전송하지 않고 입력창에 채워** 사용자가 검토·수정 후 **전송** 버튼으로 보낸다
  (`SpeechInputRelay.submitImmediately = false` 기본).

## 커밋/푸시
- 사용자가 요청할 때만 커밋·푸시한다. 커밋 전 **키/문서 등 민감 파일이 스테이징되지 않았는지** 확인한다.
