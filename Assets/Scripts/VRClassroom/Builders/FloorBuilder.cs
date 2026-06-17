using UnityEngine;
using VRClassroom.Data;

namespace VRClassroom.Builders
{
    /// <summary>바닥(나무/풀/흙/단) 생성. vr_transform 을 그대로 사용한다.</summary>
    public class FloorBuilder : IElementBuilder
    {
        public string Name => "Floors";

        public void Build(ClassroomBuildContext ctx)
        {
            if (ctx.layout.floors == null) return;
            var parent = ctx.GetParent("Floors");

            foreach (var floor in ctx.layout.floors)
            {
                if (floor?.vr_transform == null || !floor.vr_transform.IsValid)
                {
                    Debug.LogWarning($"[VRClassroom] floor '{floor?.id}' vr_transform 누락 - 건너뜀");
                    continue;
                }

                var go = BuildObjectUtil.CreateBox($"Floor_{floor.id}_{floor.type}", parent);
                var vr = floor.vr_transform;
                ctx.PlaceBox(go.transform, vr.position.ToVector3(), vr.rotation_y, vr.size.ToScale());

                // 머티리얼 (사용자 지정 우선, 없으면 type 색상)
                if (!ctx.settings.TryResolveMaterial(floor.type, out var mat))
                    mat = ctx.materials.GetColored("floor_" + floor.type, ctx.settings.ResolveColor(floor.type));
                BuildObjectUtil.ApplyMaterial(go, mat);

                if (ctx.settings.applyTags)
                    BuildObjectUtil.SetTagSafe(go, ctx.settings.floorTag);

                BuildObjectUtil.SetCollider(go, ctx.settings.floorColliders);

                go.AddComponent<ClassroomElement>().Init(floor.id, floor.type, floor.label, "floor");
            }
        }
    }
}
