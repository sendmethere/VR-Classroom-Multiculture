using System.Collections.Generic;
using UnityEngine;
using VRClassroom.Data;

namespace VRClassroom
{
    /// <summary>
    /// 한 번의 빌드 동안 모든 빌더가 공유하는 상태.
    /// (레이아웃 데이터, 설정, 머티리얼 팩토리, 부모 트랜스폼, 조회용 인덱스)
    /// </summary>
    public class ClassroomBuildContext
    {
        public readonly ClassroomLayout layout;
        public readonly ClassroomBuildSettings settings;
        public readonly MaterialFactory materials;
        public readonly Transform root;

        public readonly Dictionary<string, WallData> wallsById = new Dictionary<string, WallData>();
        public readonly Dictionary<string, List<DoorData>> doorsByWall = new Dictionary<string, List<DoorData>>();

        readonly Dictionary<string, Transform> _parents = new Dictionary<string, Transform>();

        public float Scale => settings.globalScale;

        public ClassroomBuildContext(ClassroomLayout layout, ClassroomBuildSettings settings, Transform root)
        {
            this.layout = layout;
            this.settings = settings;
            this.root = root;
            this.materials = new MaterialFactory();

            BuildIndices();
            ComputeSpace();
        }

        void BuildIndices()
        {
            if (layout.walls != null)
                foreach (var w in layout.walls)
                    if (w != null && !string.IsNullOrEmpty(w.id))
                        wallsById[w.id] = w;

            if (layout.doors != null)
            {
                foreach (var d in layout.doors)
                {
                    if (d == null || string.IsNullOrEmpty(d.wall_id)) continue;
                    if (!doorsByWall.TryGetValue(d.wall_id, out var list))
                    {
                        list = new List<DoorData>();
                        doorsByWall[d.wall_id] = list;
                    }
                    list.Add(d);
                }
            }
        }

        /// <summary>카테고리별 부모 오브젝트(없으면 생성).</summary>
        public Transform GetParent(string categoryName)
        {
            if (_parents.TryGetValue(categoryName, out var t) && t != null)
                return t;

            var go = new GameObject(categoryName);
            go.transform.SetParent(root, false);
            _parents[categoryName] = go.transform;
            return go.transform;
        }

        /// <summary>벽 높이 추정(설정 override 우선, 없으면 첫 벽의 높이, 그래도 없으면 2.8m).</summary>
        public float ResolveWallHeight()
        {
            if (settings.ceilingHeightOverride > 0f)
                return settings.ceilingHeightOverride;

            if (layout.walls != null)
                foreach (var w in layout.walls)
                    if (w?.vr_transform?.size != null && w.vr_transform.size.h > 0f)
                        return w.vr_transform.size.h * Scale;

            return 2.8f * Scale;
        }

        // -------------------------------------------------------------
        // 좌표 변환 (모든 빌더가 공유) : 스케일 → X 미러 → 원점 재배치
        //  · X 미러는 위치를 방 중심선 기준으로 반사하고 회전 Y 부호를 반전한다
        //    (음수 스케일이 아니므로 메시 법선/조명 정상).
        // -------------------------------------------------------------
        float _centerX;   // 스케일 적용된 방 중심 X
        float _centerZ;   // 스케일 적용된 방 중심 Z

        void ComputeSpace()
        {
            var room = layout.room;
            if (room != null && room.vr_width_m > 0f && room.vr_height_m > 0f)
            {
                _centerX = room.vr_width_m * 0.5f * Scale;
                _centerZ = room.vr_height_m * 0.5f * Scale;
                return;
            }

            // room 정보가 없으면 바닥(없으면 아이템) 위치의 바운딩박스 중심 사용
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool any = false;

            void Acc(Data.VrTransform vr)
            {
                if (vr == null || !vr.IsValid) return;
                any = true;
                float x = vr.position.x * Scale, z = vr.position.z * Scale;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }

            if (layout.floors != null) foreach (var f in layout.floors) Acc(f?.vr_transform);
            if (!any && layout.items != null) foreach (var it in layout.items) Acc(it?.vr_transform);

            _centerX = any ? (minX + maxX) * 0.5f : 0f;
            _centerZ = any ? (minZ + maxZ) * 0.5f : 0f;
        }

        float MapX(float x)
        {
            if (settings.mirrorX) x = 2f * _centerX - x;   // 방 중심선 기준 반사
            if (settings.recenterToOrigin) x -= _centerX;
            return x;
        }

        float MapZ(float z)
        {
            if (settings.recenterToOrigin) z -= _centerZ;
            return z;
        }

        /// <summary>이미 스케일이 적용된 월드 좌표에 X 미러/원점 재배치를 적용.</summary>
        public Vector3 MapScaledPosition(Vector3 p)
        {
            p.x = MapX(p.x);
            p.z = MapZ(p.z);
            return p;
        }

        /// <summary>X 미러 시 Y 회전 부호 반전.</summary>
        public float MapRotationY(float rotationY) => settings.mirrorX ? -rotationY : rotationY;

        /// <summary>
        /// (원본 vr 미터 공간에서) 벽 부착 아이템을 가장 가까운 벽의 '방 쪽 표면'에 밀착시킨다.
        /// 아이템이 원래 있던 쪽(벽 법선 방향)을 유지한 채, 벽 두께/2 + 아이템 두께/2 만큼만
        /// 중심선에서 떨어뜨려 표면에 딱 맞춘다(z-파이팅 방지). y 와 벽을 따라가는 좌표는 유지.
        /// </summary>
        public bool TrySnapToNearestWall(ref Vector3 posMeters, float maxDist, float itemDepth)
        {
            if (layout.walls == null) return false;

            Vector2 p = new Vector2(posMeters.x, posMeters.z);
            float bestDist = maxDist;
            bool found = false;
            Vector2 bestOnLine = default, bestDir = default;
            float bestWallThick = 0f;

            foreach (var w in layout.walls)
            {
                var vr = w?.vr_transform;
                if (vr == null || !vr.IsValid) continue;

                Vector3 axis3 = GeometryUtil.WidthAxis(vr.rotation_y);
                Vector2 axis = new Vector2(axis3.x, axis3.z);
                Vector2 c = new Vector2(vr.position.x, vr.position.z);
                float half = vr.size.w * 0.5f;

                float t = Vector2.Dot(p - c, axis);              // 벽을 따라가는 좌표
                if (t < -half - 0.2f || t > half + 0.2f) continue; // 벽 범위 밖(+여유)

                Vector2 onCenterline = c + axis * t;
                float dist = Vector2.Distance(p, onCenterline);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestOnLine = onCenterline;
                    bestWallThick = vr.size.d;
                    bestDir = (dist > 1e-3f) ? (p - onCenterline) / dist : Vector2.zero; // 아이템이 있던 쪽
                    found = true;
                }
            }

            if (!found) return false;

            Vector2 finalP = (bestDir == Vector2.zero)
                ? bestOnLine
                : bestOnLine + bestDir * (bestWallThick * 0.5f + itemDepth * 0.5f);
            posMeters.x = finalP.x;
            posMeters.z = finalP.y;
            return true;
        }

        /// <summary>
        /// 미터 단위의 vr 좌표/회전/크기를 받아 스케일·미러·재배치를 적용해 박스를 배치한다.
        /// 모든 빌더는 원본(vr) 공간에서 값을 계산한 뒤 이 메서드로만 배치해 일관성을 보장한다.
        /// </summary>
        public void PlaceBox(Transform t, Vector3 vrPositionMeters, float rotationY, Vector3 sizeMeters)
        {
            t.position = MapScaledPosition(vrPositionMeters * Scale);
            t.rotation = Quaternion.Euler(0f, MapRotationY(rotationY), 0f);
            t.localScale = sizeMeters * Scale;
        }
    }
}
