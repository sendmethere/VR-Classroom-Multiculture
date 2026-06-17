using UnityEngine;
using VRClassroom.Data;

namespace VRClassroom.Builders
{
    /// <summary>
    /// 가구/소품(책상, 의자, 칠판, 화분, 램프 등) 생성.
    /// type 문자열로 태그/색상을 결정하므로, 새 type 이 들어와도
    /// 설정의 규칙만 추가하면 대응된다.
    /// </summary>
    public class ItemBuilder : IElementBuilder
    {
        public string Name => "Items";

        public void Build(ClassroomBuildContext ctx)
        {
            if (ctx.layout.items == null) return;
            var parent = ctx.GetParent("Items");

            foreach (var item in ctx.layout.items)
            {
                if (item?.vr_transform == null || !item.vr_transform.IsValid)
                {
                    Debug.LogWarning($"[VRClassroom] item '{item?.id}' vr_transform 누락 - 건너뜀");
                    continue;
                }

                var go = BuildObjectUtil.CreateBox($"Item_{item.id}_{item.type}", parent);
                var vr = item.vr_transform;

                Vector3 pos = vr.position.ToVector3();

                // position_2d 는 '좌상단 코너' → 가로/세로의 절반을 더해 중심으로 보정
                // (책상-의자 가운데 정렬, 칠판 교실 중앙 배치의 핵심 수정)
                if (ctx.settings.itemPositionIsCorner)
                {
                    pos.x += vr.size.w * 0.5f;
                    pos.z += vr.size.d * 0.5f;
                }

                // JSON 의 아이템 y 는 밑면 기준 → 중심피벗 박스를 위로 절반 올려 바닥에 맞춤
                if (ctx.settings.anchorItemsToFloor)
                    pos.y += vr.size.h * 0.5f;

                // 벽 부착 아이템(칠판/캔버스 등)을 가장 가까운 벽면에 밀착(미세 정렬)
                if (ctx.settings.IsWallSnapType(item.type))
                    ctx.TrySnapToNearestWall(ref pos, ctx.settings.wallSnapMaxDistance, vr.size.d);

                ctx.PlaceBox(go.transform, pos, vr.rotation_y, vr.size.ToScale());

                if (!ctx.settings.TryResolveMaterial(item.type, out var mat))
                    mat = ctx.materials.GetColored("item_" + item.type, ctx.settings.ResolveColor(item.type));
                BuildObjectUtil.ApplyMaterial(go, mat);

                if (ctx.settings.applyTags)
                    BuildObjectUtil.SetTagSafe(go, ctx.settings.ResolveItemTag(item.type));

                BuildObjectUtil.SetCollider(go, ctx.settings.itemColliders);

                go.AddComponent<ClassroomElement>().Init(item.id, item.type, item.label, "item");
            }
        }
    }
}
