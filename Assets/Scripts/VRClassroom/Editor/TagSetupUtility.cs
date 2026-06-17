using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRClassroom.EditorTools
{
    /// <summary>
    /// 빌드에 필요한 태그를 프로젝트(TagManager)에 자동 생성한다.
    /// 런타임에서는 태그를 만들 수 없으므로, 에디터 빌드/메뉴에서 미리 호출한다.
    /// </summary>
    public static class TagSetupUtility
    {
        // 기본 + 아이템 카테고리 태그
        static readonly string[] DefaultTags =
        {
            "Floor", "Wall", "Ceiling", "Door",
            "Chair", "Desk", "Table", "Blackboard",
            "Podium", "Plant", "Lamp", "Prop",
        };

        [MenuItem("Tools/VR Classroom/Setup Tags")]
        public static void SetupDefaultTags()
        {
            EnsureTags(DefaultTags);
            Debug.Log("[VRClassroom] 태그 세팅 완료.");
        }

        /// <summary>설정 에셋에 정의된 태그까지 포함해 모두 보장.</summary>
        public static void EnsureTagsFromSettings(ClassroomBuildSettings settings)
        {
            var tags = new List<string>(DefaultTags);
            if (settings != null)
            {
                tags.Add(settings.floorTag);
                tags.Add(settings.wallTag);
                tags.Add(settings.ceilingTag);
                tags.Add(settings.doorTag);
                tags.Add(settings.defaultItemTag);
                if (settings.itemTagRules != null)
                    foreach (var r in settings.itemTagRules)
                        if (!string.IsNullOrEmpty(r.tag)) tags.Add(r.tag);
            }
            EnsureTags(tags);
        }

        public static void EnsureTags(IEnumerable<string> tags)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[VRClassroom] TagManager.asset 을 찾지 못했습니다.");
                return;
            }

            var so = new SerializedObject(asset[0]);
            SerializedProperty tagsProp = so.FindProperty("tags");

            foreach (var tag in tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                if (TagExists(tagsProp, tag)) continue;

                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            }

            so.ApplyModifiedProperties();
        }

        static bool TagExists(SerializedProperty tagsProp, string tag)
        {
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return true;
            return false;
        }
    }
}
