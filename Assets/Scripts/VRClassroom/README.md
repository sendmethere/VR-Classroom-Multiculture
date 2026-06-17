# VR Classroom Builder

`StreamingAssets/classroom_layout_*.json` 을 읽어 Unity 씬에 교실 공간을 생성하는 스크립트 모음입니다.
바닥/벽/문/천장/가구를 만들고, 태그·머티리얼·콜라이더를 적용합니다.

## 빠른 사용법 (에디터)
1. 빈 GameObject 에 `ClassroomBuilder` 컴포넌트를 추가합니다.
2. 인스펙터에서 **불러올 파일 선택** → StreamingAssets 드롭다운에서 JSON 선택
   (또는 **Browse...** 로 임의 위치의 JSON 선택).
3. **Build (씬 생성)** 클릭. → 필요한 태그가 자동 생성되고 공간이 만들어집니다.
   다시 누르면 이전 결과를 지우고 새로 만듭니다. **Clear** 로 비울 수 있습니다.

## 빠른 사용법 (런타임 / VR)
- `ClassroomBuilder` 와 함께 `ClassroomRuntimeMenu` 를 같은 오브젝트에 추가하면,
  플레이 중 화면 좌측 상단에 파일 선택 UI 가 뜹니다(F1 토글).
- 또는 코드에서 `builder.BuildFromStreamingAssets("파일이름.json")` 호출.
- ※ 런타임 태그는 생성 불가하므로, 빌드 전에 메뉴 **Tools ▸ VR Classroom ▸ Setup Tags**
  를 한 번 실행해 태그를 만들어 두세요(에디터 Build 버튼은 자동 처리).

## 설정 커스터마이즈
- 메뉴 **Assets ▸ Create ▸ VR Classroom ▸ Build Settings** 로 설정 에셋을 만들고
  `ClassroomBuilder.settings` 에 지정하면 태그/머티리얼/색상/천장 규칙을 바꿀 수 있습니다.
- 미지정 시 합리적인 기본값이 자동 적용됩니다.

## 좌표 보정 (Build Settings)
- **itemPositionIsCorner** (기본 ON, ★가장 중요): JSON 아이템의 `position_2d` 는 오브젝트의
  *중심이 아니라 좌상단 코너*입니다. 가로/세로의 절반을 더해 중심으로 보정합니다.
  이 보정이 없으면 **의자가 책상 가운데에 안 맞고, 칠판이 교실 중앙을 벗어나 벽에 파묻히며,
  벽 장식이 떠 보입니다**. (벽/바닥은 이미 중심 기준이라 보정하지 않음)
- **mirrorX** (기본 ON): X축 좌우 반전. 2D 도면과 좌우가 뒤집혀 나올 때 사용.
  음수 스케일이 아니라 *위치 반사 + 회전 Y 부호 반전*으로 처리하므로 메시 법선/조명이 정상이고,
  문 뚫린 위치까지 정확히 함께 미러됩니다.
- **recenterToOrigin** (기본 ON): `room` 크기(기본 70×30m) 기준으로 레이아웃 중심을 월드 원점에 맞춤.
- **anchorItemsToFloor** (기본 ON): JSON 의 아이템 `y` 는 *밑면* 기준이라, 중심피벗 박스를
  높이의 절반만큼 올려 바닥/벽 부착 높이를 맞춥니다(책상 파묻힘·칠판 낮음 현상 해결).
  ※ 벽·바닥·천장은 JSON 에서 이미 중심 기준이라 보정하지 않습니다.

## 동작 규칙
- **좌표**: JSON 의 `vr_transform`(미터 단위)을 기준으로, 위 보정 옵션을 거쳐 배치합니다.
- **문 뚫기**: `door.wall_id` 로 해당 벽을 찾아 그 구간을 비우고 벽을 세그먼트로 나눕니다.
- **천장**: `settings.ceilingFloorTypes`(기본 `wood`=나무바닥)에 해당하는,
  벽으로 구획된 공간 위에만 만듭니다(실외 풀/흙 바닥은 제외).
- **벽 충돌**: 벽에는 콜라이더가 있어 통과할 수 없습니다(문은 통과 가능).
- **태그**: 바닥=Floor, 벽=Wall, 천장=Ceiling, 문=Door,
  의자=Chair, 책상=Desk, 테이블=Table, 칠판=Blackboard 등 type 기준 자동 부여.

## 확장 방법
새 종류의 오브젝트(창문, 실제 여닫는 문, 프리팹 배치 등)를 추가하려면
`Builders/IElementBuilder` 를 구현한 클래스를 만들고,
`ClassroomBuilder.CreateBuilders()` 의 목록에 추가하면 됩니다.

## 파일 구성
- `ClassroomLayoutModels.cs` — JSON 직렬화 모델
- `ClassroomJsonLoader.cs` — 파일 로드/파싱(데스크톱 동기 + Android UnityWebRequest)
- `ClassroomBuildSettings.cs` — 설정 ScriptableObject(태그/색상/규칙)
- `BuildUtil.cs` — 기하/머티리얼/오브젝트 헬퍼
- `ClassroomBuildContext.cs` — 빌드 중 공유 상태/인덱스
- `ClassroomElement.cs` — 생성물에 붙는 메타데이터
- `Builders/` — Floor / Wall / Door / Ceiling / Item 빌더 (+ `IElementBuilder`)
- `ClassroomBuilder.cs` — 오케스트레이터(MonoBehaviour)
- `ClassroomRuntimeMenu.cs` — 런타임 파일 선택 UI
- `Editor/` — 커스텀 인스펙터 + 태그 자동 생성
