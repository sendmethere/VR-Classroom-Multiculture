using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRClassroom
{
    /// <summary>
    /// 빌드 동작 전반을 제어하는 설정. ScriptableObject 라서 에셋으로 만들어
    /// 인스펙터에서 태그/머티리얼/색상/천장 규칙 등을 자유롭게 바꿀 수 있다.
    /// 에셋을 지정하지 않으면 <see cref="CreateRuntimeDefault"/> 의 기본값이 쓰인다.
    /// (확장 포인트: 규칙 리스트에 항목을 추가하기만 하면 새 타입을 지원)
    /// </summary>
    [CreateAssetMenu(menuName = "VR Classroom/Build Settings", fileName = "ClassroomBuildSettings")]
    public class ClassroomBuildSettings : ScriptableObject
    {
        [Serializable]
        public class TypeTagRule
        {
            [Tooltip("type 문자열에 이 값이 포함되면 적용")] public string typeContains;
            public string tag;
        }

        [Serializable]
        public class TypeMaterialRule
        {
            public string typeContains;
            public Material material;
        }

        [Serializable]
        public class TypeColorRule
        {
            public string typeContains;
            public Color color = Color.white;
        }

        [Header("스케일")]
        [Tooltip("vr_transform 값이 이미 미터 단위이므로 1 권장")]
        public float globalScale = 1f;

        [Header("좌표 보정")]
        [Tooltip("X축을 좌우 반전해 생성 (2D 도면과 좌우가 뒤집혀 나올 때 켜기). 위치 미러 + 회전 부호 반전으로 처리하므로 메시 법선은 정상.")]
        public bool mirrorX = true;
        [Tooltip("레이아웃 중심을 월드 원점(0,0,0)으로 옮겨 배치")]
        public bool recenterToOrigin = true;
        [Tooltip("아이템(가구/소품)을 밑면이 바닥(y=0)에 닿도록 올림. JSON 의 아이템 y 는 밑면 기준이라 중심피벗 박스는 보정 필요.")]
        public bool anchorItemsToFloor = true;
        [Tooltip("JSON 아이템의 position_2d 는 '좌상단 코너' 라서 중심으로 보정(+가로/2, +세로/2). 끄면 코너 그대로 → 책상·의자·칠판이 어긋남. 거의 항상 ON 권장.")]
        public bool itemPositionIsCorner = true;

        [Header("벽 부착 스냅")]
        [Tooltip("칠판/캔버스/메모판/벽램프 등 벽 부착 아이템을 가장 가까운 벽 중심면에 딱 붙임(데이터의 미세 오프셋 보정)")]
        public bool snapWallItemsToWall = true;
        [Tooltip("이 문자열이 type 에 포함되면 벽 스냅 대상")]
        public string[] wallSnapTypes = { "blackboard", "canvas", "memo", "lamp_wall" };
        [Tooltip("이 거리(m) 안에 벽이 있을 때만 스냅. 코너 보정 후엔 미세 정렬용이라 작게(0.4) 권장")]
        public float wallSnapMaxDistance = 0.4f;

        [Header("천장 (구획된 공간에만)")]
        [Tooltip("이 type 의 바닥 위에만 천장을 만든다. 기본: 나무바닥(wood)")]
        public string[] ceilingFloorTypes = { "wood" };
        public float ceilingThickness = 0.1f;
        [Tooltip("0 이하이면 벽 높이를 자동으로 사용")]
        public float ceilingHeightOverride = 0f;
        [Tooltip("천장을 바닥 footprint 보다 살짝 키워 벽과 겹치게 (m)")]
        public float ceilingPadding = 0.15f;

        [Header("문")]
        [Tooltip("문 위치에 태그용 마커 오브젝트 생성")]
        public bool buildDoorMarkers = true;
        [Tooltip("문 마커를 실제로 보이게 할지. 기본 false = 열린 출입구(메시 숨김, 태그만 유지)")]
        public bool doorMarkerVisible = false;
        [Tooltip("문에 콜라이더를 둘지 (기본 false = 통과 가능)")]
        public bool doorHasCollider = false;

        [Header("태그")]
        public bool applyTags = true;
        public string floorTag = "Floor";
        public string wallTag = "Wall";
        public string ceilingTag = "Ceiling";
        public string doorTag = "Door";
        [Tooltip("아이템 type → 태그 규칙 (위에서부터 먼저 매칭)")]
        public List<TypeTagRule> itemTagRules = new List<TypeTagRule>();
        public string defaultItemTag = "Prop";

        [Header("머티리얼 / 색상")]
        public List<TypeMaterialRule> materialRules = new List<TypeMaterialRule>();
        [Tooltip("머티리얼 미지정 시 적용할 기본 색")]
        public List<TypeColorRule> colorRules = new List<TypeColorRule>();
        public Color fallbackColor = new Color(0.8f, 0.8f, 0.8f);

        [Header("콜라이더")]
        public bool floorColliders = true;
        public bool wallColliders = true;   // "벽은 뚫고 가지 못하게" → true 유지
        public bool itemColliders = true;

        // -------------------------------------------------------------
        // 규칙 해석 헬퍼
        // -------------------------------------------------------------

        public string ResolveItemTag(string type)
        {
            string t = type ?? "";
            foreach (var r in itemTagRules)
                if (!string.IsNullOrEmpty(r.typeContains) && t.Contains(r.typeContains))
                    return r.tag;
            return defaultItemTag;
        }

        public bool TryResolveMaterial(string type, out Material mat)
        {
            string t = type ?? "";
            foreach (var r in materialRules)
            {
                if (r.material != null && !string.IsNullOrEmpty(r.typeContains) && t.Contains(r.typeContains))
                {
                    mat = r.material;
                    return true;
                }
            }
            mat = null;
            return false;
        }

        public Color ResolveColor(string type)
        {
            string t = type ?? "";
            foreach (var r in colorRules)
                if (!string.IsNullOrEmpty(r.typeContains) && t.Contains(r.typeContains))
                    return r.color;
            return fallbackColor;
        }

        public bool IsWallSnapType(string type)
        {
            if (!snapWallItemsToWall || wallSnapTypes == null) return false;
            string t = type ?? "";
            foreach (var s in wallSnapTypes)
                if (!string.IsNullOrEmpty(s) && t.Contains(s))
                    return true;
            return false;
        }

        public bool IsCeilingFloorType(string floorType)
        {
            if (ceilingFloorTypes == null) return false;
            string t = floorType ?? "";
            foreach (var c in ceilingFloorTypes)
                if (!string.IsNullOrEmpty(c) && t.Contains(c))
                    return true;
            return false;
        }

        // -------------------------------------------------------------
        // 에셋이 없을 때 사용할 합리적인 기본 설정
        // -------------------------------------------------------------
        public static ClassroomBuildSettings CreateRuntimeDefault()
        {
            var s = CreateInstance<ClassroomBuildSettings>();
            s.name = "ClassroomBuildSettings (Runtime Default)";

            s.itemTagRules = new List<TypeTagRule>
            {
                new TypeTagRule { typeContains = "chair", tag = "Chair" },
                new TypeTagRule { typeContains = "desk",  tag = "Desk"  },
                new TypeTagRule { typeContains = "table", tag = "Table" },
                new TypeTagRule { typeContains = "blackboard", tag = "Blackboard" },
                new TypeTagRule { typeContains = "podium", tag = "Podium" },
                new TypeTagRule { typeContains = "plant", tag = "Plant" },
                new TypeTagRule { typeContains = "lamp",  tag = "Lamp"  },
                new TypeTagRule { typeContains = "memo",  tag = "Prop"  },
                new TypeTagRule { typeContains = "canvas", tag = "Prop" },
            };

            s.colorRules = new List<TypeColorRule>
            {
                // 바닥
                new TypeColorRule { typeContains = "wood",  color = new Color(0.55f, 0.40f, 0.25f) },
                new TypeColorRule { typeContains = "grass", color = new Color(0.30f, 0.65f, 0.30f) },
                new TypeColorRule { typeContains = "dirt",  color = new Color(0.70f, 0.55f, 0.35f) },
                new TypeColorRule { typeContains = "step",  color = new Color(0.60f, 0.50f, 0.40f) },
                // 구조
                new TypeColorRule { typeContains = "wall",    color = new Color(0.85f, 0.85f, 0.85f) },
                new TypeColorRule { typeContains = "ceiling", color = new Color(0.95f, 0.95f, 0.95f) },
                new TypeColorRule { typeContains = "door",    color = new Color(0.60f, 0.42f, 0.28f) },
                // 가구
                new TypeColorRule { typeContains = "chair", color = new Color(0.35f, 0.50f, 0.80f) },
                new TypeColorRule { typeContains = "desk",  color = new Color(0.60f, 0.45f, 0.30f) },
                new TypeColorRule { typeContains = "table", color = new Color(0.65f, 0.50f, 0.35f) },
                new TypeColorRule { typeContains = "blackboard", color = new Color(0.12f, 0.32f, 0.22f) },
                new TypeColorRule { typeContains = "podium", color = new Color(0.50f, 0.40f, 0.30f) },
                new TypeColorRule { typeContains = "plant",  color = new Color(0.20f, 0.60f, 0.25f) },
                new TypeColorRule { typeContains = "lamp",   color = new Color(0.95f, 0.90f, 0.60f) },
                new TypeColorRule { typeContains = "memo",   color = new Color(0.95f, 0.95f, 0.85f) },
                new TypeColorRule { typeContains = "canvas", color = new Color(0.90f, 0.88f, 0.80f) },
            };
            return s;
        }
    }
}
