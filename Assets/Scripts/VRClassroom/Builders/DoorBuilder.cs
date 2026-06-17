using UnityEngine;
using VRClassroom.Data;

namespace VRClassroom.Builders
{
    /// <summary>
    /// 문 마커 생성. 벽은 WallBuilder 가 이미 뚫어 두므로 통로 자체는 비어 있다.
    /// 여기서는 문 위치를 표시/식별할 얇은 문틀(기본적으로 콜라이더 없음 = 통과 가능)을 만든다.
    /// 실제로 여닫는 문이 필요하면 이 빌더를 교체/확장하면 된다.
    /// </summary>
    public class DoorBuilder : IElementBuilder
    {
        public string Name => "Doors";

        public void Build(ClassroomBuildContext ctx)
        {
            if (ctx.layout.doors == null || !ctx.settings.buildDoorMarkers) return;
            var parent = ctx.GetParent("Doors");

            foreach (var door in ctx.layout.doors)
            {
                if (door?.vr_transform == null || !door.vr_transform.IsValid)
                    continue;

                var go = BuildObjectUtil.CreateBox($"Door_{door.id}", parent);
                var vr = door.vr_transform;
                // 벽과 같은 두께로 개구부에 딱 맞춤(파임/돌출 없음)
                ctx.PlaceBox(go.transform, vr.position.ToVector3(), vr.rotation_y, vr.size.ToScale());

                if (!ctx.settings.TryResolveMaterial("door", out var mat))
                    mat = ctx.materials.GetColored("door", ctx.settings.ResolveColor("door"));
                BuildObjectUtil.ApplyMaterial(go, mat);

                // 기본은 열린 출입구: 메시는 숨기고(태그/위치만 유지) 통과 가능
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.enabled = ctx.settings.doorMarkerVisible;

                if (ctx.settings.applyTags)
                    BuildObjectUtil.SetTagSafe(go, ctx.settings.doorTag);

                // 기본은 통과 가능(콜라이더 제거). doorHasCollider=true 면 콜라이더 유지.
                BuildObjectUtil.SetCollider(go, ctx.settings.doorHasCollider);

                go.AddComponent<ClassroomElement>().Init(door.id, "door", null, "door");
            }
        }
    }
}
