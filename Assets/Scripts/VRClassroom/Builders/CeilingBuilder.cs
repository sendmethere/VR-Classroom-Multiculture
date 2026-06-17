using UnityEngine;
using VRClassroom.Data;

namespace VRClassroom.Builders
{
    /// <summary>
    /// 천장 생성. 요구사항대로 "벽/나무바닥으로 구획된 공간"에만 만든다.
    /// 즉 설정의 ceilingFloorTypes(기본: wood) 에 해당하는 바닥의 footprint 위에만
    /// 벽 높이에 맞춰 천장 슬래브를 덮는다.
    /// </summary>
    public class CeilingBuilder : IElementBuilder
    {
        public string Name => "Ceiling";

        public void Build(ClassroomBuildContext ctx)
        {
            if (ctx.layout.floors == null) return;

            var parent = ctx.GetParent("Ceiling");
            float scale = ctx.Scale;
            float wallHeight = ctx.ResolveWallHeight();
            float thickness = ctx.settings.ceilingThickness * scale;
            float pad = ctx.settings.ceilingPadding * scale;

            foreach (var floor in ctx.layout.floors)
            {
                if (floor?.vr_transform == null || !floor.vr_transform.IsValid) continue;
                if (!ctx.settings.IsCeilingFloorType(floor.type)) continue;   // 구획된(나무바닥) 공간만

                var vr = floor.vr_transform;
                var go = BuildObjectUtil.CreateBox($"Ceiling_{floor.id}", parent);

                Vector3 footprint = vr.position.ToVector3() * scale;
                Vector3 pos = new Vector3(footprint.x, wallHeight + thickness * 0.5f, footprint.z);
                go.transform.position = ctx.MapScaledPosition(pos);     // X 미러/재배치 적용
                go.transform.rotation = Quaternion.Euler(0f, ctx.MapRotationY(vr.rotation_y), 0f);
                go.transform.localScale = new Vector3(vr.size.w * scale + pad, thickness, vr.size.d * scale + pad);

                if (!ctx.settings.TryResolveMaterial("ceiling", out var mat))
                    mat = ctx.materials.GetColored("ceiling", ctx.settings.ResolveColor("ceiling"));
                BuildObjectUtil.ApplyMaterial(go, mat);

                if (ctx.settings.applyTags)
                    BuildObjectUtil.SetTagSafe(go, ctx.settings.ceilingTag);

                BuildObjectUtil.SetCollider(go, true);

                go.AddComponent<ClassroomElement>().Init("ceiling_" + floor.id, "ceiling", null, "ceiling");
            }
        }
    }
}
