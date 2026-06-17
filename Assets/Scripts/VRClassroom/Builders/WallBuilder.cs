using System.Collections.Generic;
using UnityEngine;
using VRClassroom.Data;

namespace VRClassroom.Builders
{
    /// <summary>
    /// 벽 생성. 해당 벽(wall_id)에 연결된 문이 있으면 그 구간을 비워(=문을 뚫어)
    /// 벽을 여러 개의 솔리드 세그먼트로 나눠 만든다. 문 높이가 벽 높이보다 낮으면
    /// 문 위에 인방(header)을 추가한다. 벽에는 콜라이더가 있어 통과할 수 없다.
    ///
    /// 모든 계산은 원본 vr(미터) 공간에서 수행하고, 최종 배치는 ctx.PlaceBox 로만
    /// 한다. 따라서 X 미러/원점 재배치가 켜져 있어도 문 위치까지 정확히 미러된다.
    /// </summary>
    public class WallBuilder : IElementBuilder
    {
        public string Name => "Walls";

        struct Opening
        {
            public float a;        // 시작(벽 로컬 축, m)
            public float b;        // 끝
            public float doorH;    // 문 높이(m)
        }

        public void Build(ClassroomBuildContext ctx)
        {
            if (ctx.layout.walls == null) return;
            var parent = ctx.GetParent("Walls");

            foreach (var wall in ctx.layout.walls)
                BuildWall(ctx, wall, parent);
        }

        void BuildWall(ClassroomBuildContext ctx, WallData wall, Transform parent)
        {
            var vr = wall?.vr_transform;
            if (vr == null || !vr.IsValid)
            {
                Debug.LogWarning($"[VRClassroom] wall '{wall?.id}' vr_transform 누락 - 건너뜀");
                return;
            }

            // --- 모두 원본 vr 미터 단위로 계산 ---
            float length = vr.size.w;       // 벽 길이
            float height = vr.size.h;       // 벽 높이
            float thickness = vr.size.d;    // 벽 두께
            Vector3 center = vr.position.ToVector3();
            Vector3 axis = GeometryUtil.WidthAxis(vr.rotation_y);   // 원본 회전 기준 가로축
            float half = length * 0.5f;

            // --- 이 벽의 문들을 벽 로컬 축 좌표(-half..half) 의 개구부로 변환 ---
            var openings = new List<Opening>();
            if (ctx.doorsByWall.TryGetValue(wall.id, out var doors))
            {
                foreach (var d in doors)
                {
                    if (d?.vr_transform?.position == null) continue;
                    Vector3 dc = d.vr_transform.position.ToVector3();
                    float s = Vector3.Dot(dc - center, axis);            // 문 중심의 축 위치
                    float dw = (d.vr_transform.size != null) ? d.vr_transform.size.w : d.width_cm / 100f;
                    float dh = (d.vr_transform.size != null) ? d.vr_transform.size.h : d.height_cm / 100f;

                    float a = Mathf.Clamp(s - dw * 0.5f, -half, half);
                    float b = Mathf.Clamp(s + dw * 0.5f, -half, half);
                    if (b - a > 0.001f)
                        openings.Add(new Opening { a = a, b = b, doorH = dh });
                }
                openings.Sort((p, q) => p.a.CompareTo(q.a));
            }

            // --- 개구부 사이를 솔리드 세그먼트로 채운다 ---
            float cursor = -half;
            int seg = 0;
            foreach (var op in openings)
            {
                if (op.a > cursor + 0.001f)
                    CreateSegment(ctx, wall, parent, center, axis, cursor, op.a,
                                  height, thickness, vr.rotation_y, center.y, ref seg);

                // 문 위 인방 (문이 벽보다 낮을 때만)
                float headerH = height - op.doorH;
                if (headerH > 0.01f)
                {
                    float bottom = center.y - height * 0.5f;
                    float headerCenterY = bottom + op.doorH + headerH * 0.5f;
                    CreateBox(ctx, wall, parent, center, axis,
                              (op.a + op.b) * 0.5f, op.b - op.a, headerH, thickness,
                              vr.rotation_y, headerCenterY, $"{wall.id}_header{seg}");
                    seg++;
                }

                cursor = Mathf.Max(cursor, op.b);
            }

            if (cursor < half - 0.001f)
                CreateSegment(ctx, wall, parent, center, axis, cursor, half,
                              height, thickness, vr.rotation_y, center.y, ref seg);
        }

        void CreateSegment(ClassroomBuildContext ctx, WallData wall, Transform parent,
                           Vector3 center, Vector3 axis, float a, float b,
                           float height, float thickness, float rotY, float centerY, ref int seg)
        {
            CreateBox(ctx, wall, parent, center, axis,
                      (a + b) * 0.5f, b - a, height, thickness, rotY, centerY,
                      $"{wall.id}_seg{seg}");
            seg++;
        }

        void CreateBox(ClassroomBuildContext ctx, WallData wall, Transform parent,
                       Vector3 center, Vector3 axis, float offset, float length,
                       float height, float thickness, float rotY, float centerY, string name)
        {
            if (length <= 0.001f) return;

            var go = BuildObjectUtil.CreateBox($"Wall_{name}", parent);

            // 세그먼트 중심(원본 vr 미터) → PlaceBox 가 스케일·미러·재배치를 적용
            Vector3 pos = center + axis * offset;
            pos.y = centerY;
            ctx.PlaceBox(go.transform, pos, rotY, new Vector3(length, height, thickness));

            if (!ctx.settings.TryResolveMaterial("wall", out var mat))
                mat = ctx.materials.GetColored("wall", ctx.settings.ResolveColor("wall"));
            BuildObjectUtil.ApplyMaterial(go, mat);

            if (ctx.settings.applyTags)
                BuildObjectUtil.SetTagSafe(go, ctx.settings.wallTag);

            BuildObjectUtil.SetCollider(go, ctx.settings.wallColliders);

            go.AddComponent<ClassroomElement>().Init(wall.id, "wall", null, "wall");
        }
    }
}
