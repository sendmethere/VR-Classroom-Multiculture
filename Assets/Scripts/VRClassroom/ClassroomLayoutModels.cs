using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRClassroom.Data
{
    // ------------------------------------------------------------------
    // classroom_layout_*.json 의 구조를 그대로 매핑한 직렬화 모델.
    // Unity 의 JsonUtility 는 "필드 이름 == JSON 키" 가 정확히 일치해야 하므로
    // 일부 필드는 snake_case 그대로 둔다. JSON 에 있지만 여기에 없는 키
    // (예: zones) 는 자동으로 무시된다.
    // ------------------------------------------------------------------

    /// <summary>JSON 최상위 래퍼: { "classroom_layout": { ... } }</summary>
    [Serializable]
    public class LayoutRoot
    {
        public ClassroomLayout classroom_layout;
    }

    [Serializable]
    public class ClassroomLayout
    {
        public string version;
        public RoomInfo room;
        public List<FloorData> floors = new List<FloorData>();
        public List<WallData> walls = new List<WallData>();
        public List<DoorData> doors = new List<DoorData>();
        public List<ItemData> items = new List<ItemData>();
    }

    [Serializable]
    public class RoomInfo
    {
        public float width_cm;
        public float height_cm;
        public float vr_width_m;
        public float vr_height_m;
    }

    // ---- 공통 값 타입 ----------------------------------------------------

    [Serializable]
    public class Vec2Cm { public float x; public float y; }

    [Serializable]
    public class SizeCm { public float w; public float h; }

    [Serializable]
    public class Pos2DCm { public float x_cm; public float y_cm; }

    [Serializable]
    public class Vec3Json
    {
        public float x, y, z;
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public class Size3Json
    {
        public float w, h, d;
        public Vector3 ToScale() => new Vector3(w, h, d);
    }

    /// <summary>이미 미터 단위(Unity 좌표)로 계산되어 있는 변환 정보.</summary>
    [Serializable]
    public class VrTransform
    {
        public Vec3Json position;
        public Size3Json size;
        public float rotation_y;
        public int render_order;
        public float depth_offset;

        public bool IsValid => position != null && size != null;
    }

    // ---- 요소별 모델 -----------------------------------------------------

    [Serializable]
    public class FloorData
    {
        public string id;
        public string type;     // wood / grass / dirt / step_15 / step_30 / step_45 ...
        public string label;
        public Vec2Cm position_cm;
        public SizeCm size_cm;
        public float raised_cm;
        public VrTransform vr_transform;
    }

    [Serializable]
    public class WallData
    {
        public string id;
        public Vec2Cm start_cm;
        public Vec2Cm end_cm;
        public float length_cm;
        public float thickness_cm;
        public float height_cm;
        public VrTransform vr_transform;
    }

    [Serializable]
    public class DoorData
    {
        public string id;
        public string wall_id;      // 어느 벽에 뚫리는지
        public float width_cm;
        public float height_cm;
        public Pos2DCm position_2d;
        public string swing;
        public VrTransform vr_transform;
    }

    [Serializable]
    public class ItemData
    {
        public string id;
        public string type;     // blackboard / desk_student / chair_round / plant / lamp_wall ...
        public string label;
        public Pos2DCm position_2d;
        public SizeCm size_cm;
        public float rotation_deg;
        public string lock_type;
        public VrTransform vr_transform;
        public string model_id;
        public string vr_shape;     // 현재는 모두 "box"
        public string collision_note;
    }
}
