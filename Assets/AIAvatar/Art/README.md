# 그래픽 에셋 가이드 (Figma → Unity)

> placeholder 원통과 코드 생성 UI는 그래픽이 **없어도 동작**합니다.
> 아래는 보기 좋게 다듬을 때의 권장 사항입니다. 만든 파일을 지정 폴더에 넣고
> 임포트 설정만 맞추면, UI 컴포넌트의 Source Image / Sprite 슬롯에 끌어다 쓰면 됩니다.

## 폴더 구조 (여기에 넣어주세요)

```
Assets/AIAvatar/Art/
  UI/          ← 패널 배경, 버튼, 입력창 배경 등 9-slice 스프라이트
  Icons/
    Emotions/  ← 감정 아이콘 (파일명 = 감정 키와 동일하게)
  Portraits/   ← (선택) 아바타 얼굴/초상 이미지
  Fonts/       ← TMP 폰트 에셋 (한글 폰트는 여기 자동 생성됨)
```

## 포맷

| 용도 | 추천 포맷 | 비고 |
|---|---|---|
| 버튼/패널/아이콘 | **PNG (32bit, 알파 포함)** | 가장 무난. Figma에서 "Export PNG" |
| 확대/축소 많은 벡터 | **SVG** | 이 프로젝트엔 Vector Graphics 패키지가 있어 SVG도 Sprite로 임포트 가능 |
| 사진/배경 | PNG 또는 JPG | 알파 불필요하면 JPG |

## 권장 크기 (월드 캔버스 설계 해상도 = 640 × 820 px)

Figma는 **2배(@2x)** 로 내보내면 VR에서 선명합니다.

| 요소 | 디자인 크기(px) | @2x 내보내기 |
|---|---|---|
| 패널 배경 | 640 × 820 | 1280 × 1640 |
| 선택지 버튼 | 592 × 56 | 1184 × 112 |
| 전송 버튼 | 120 × 66 | 240 × 132 |
| 입력창 배경 | 가변 폭 × 66 | 9-slice 권장 |
| 감정 아이콘 | 64 × 64 또는 128 × 128 | 정사각형 |
| 아바타 초상(선택) | 256 × 256 | 정사각형 |

## 9-slice (패널·버튼 배경)

모서리가 둥근 배경은 **9-slice**로 만들면 크기가 변해도 안 늘어납니다.
1. Figma에서 모서리/테두리 두께가 일정하게 디자인
2. 임포트 후 Sprite Editor에서 **Border**(L/T/R/B)를 모서리 안쪽으로 지정
3. UI Image의 **Image Type = Sliced**

## Unity 임포트 설정 (스프라이트)

파일 선택 → Inspector:
- **Texture Type**: `Sprite (2D and UI)`
- **Sprite Mode**: `Single`
- **Alpha Is Transparency**: ✔ (PNG 알파일 때)
- **Mesh Type**: `Full Rect` (UI 권장)
- **Pixels Per Unit**: 100 (기본)
- 모바일/Quest면 **Compression**: Normal~High, **Generate Mip Maps**: 끔(UI)

## 감정 아이콘 파일명 규칙 (중요)

`AvatarDirectives.emotion` 문자열과 **파일명을 동일하게** 맞춰주세요. 그래야 나중에
감정 아이콘 핸들러를 붙이면 코드 수정 없이 자동 매칭됩니다.

```
Icons/Emotions/neutral.png
Icons/Emotions/happy.png
Icons/Emotions/sad.png
Icons/Emotions/angry.png
Icons/Emotions/surprised.png
Icons/Emotions/thinking.png
```

## 있으면 좋은 에셋 목록 (우선순위 순)

1. **패널 배경** (9-slice) — 가독성에 가장 큰 영향
2. **선택지 버튼 배경** (9-slice, 한 장이면 충분 — hover/press는 색 틴트로 처리 가능)
3. **전송 버튼 / 입력창 배경**
4. **감정 아이콘 6종** (위 파일명 규칙)
5. (선택) **아바타 초상**, **말풍선**, **로딩/생각 중 스피너**

## 캐릭터 3D 모델은?

지금은 원통이라 3D 그래픽이 필요 없습니다. 나중에 리깅된 캐릭터(.fbx/.glb)를 넣을 때:
- `Assets/AIAvatar/Art/Characters/` 같은 폴더에 두고
- `Avatar (Cylinder)`를 모델로 교체한 뒤 `AvatarCharacter.bodyRenderer`(또는 핸들러)만 다시 연결
- 표정/동작은 `AvatarDirectiveHandler`를 상속해 추가 (README 상위 문서 참고)
