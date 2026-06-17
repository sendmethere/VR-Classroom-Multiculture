using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIAvatar.EditorTools
{
    /// <summary>
    /// Find scene objects by name or tag and replace them all with a chosen prefab.
    /// Keeps each object's world position, parent, and sibling order; rotation and
    /// scale can be kept, set, offset, or taken from the prefab. Fully undoable.
    /// Menu: Tools ▸ AI Avatar ▸ Batch Prefab Replacer.
    /// </summary>
    public class PrefabBatchReplacer : EditorWindow
    {
        private enum SearchBy { Name, Tag }
        private enum NameMatch { Contains, Exact, StartsWith, EndsWith }
        private enum Scope { ActiveScene, Selection, SelectionAndChildren }
        private enum RotMode { KeepOriginal, SetEuler, AddEuler, UsePrefab }
        private enum ScaleMode { KeepOriginal, SetScale, MultiplyScale, UsePrefab }

        private SearchBy searchBy = SearchBy.Name;
        private NameMatch nameMatch = NameMatch.Contains;
        private string nameQuery = "";
        private bool caseSensitive = false;
        private string tag = "Untagged";
        private Scope scope = Scope.ActiveScene;
        private bool includeInactive = true;

        private GameObject prefab;

        private Vector3 positionOffset = Vector3.zero;
        private RotMode rotMode = RotMode.KeepOriginal;
        private Vector3 rotEuler = Vector3.zero;
        private ScaleMode scaleMode = ScaleMode.KeepOriginal;
        private Vector3 scaleValue = Vector3.one;
        private bool keepName = true;

        private readonly List<GameObject> matches = new();
        private Vector2 scroll;

        [MenuItem("Tools/AI Avatar/Batch Prefab Replacer")]
        private static void Open() => GetWindow<PrefabBatchReplacer>("Prefab Replacer");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "이름/태그로 찾은 오브젝트들을 prefab으로 일괄 교체합니다. " +
                "원래 위치·부모·순서는 유지, 회전/스케일은 아래에서 지정. Ctrl+Z로 되돌리기 가능.",
                MessageType.Info);

            EditorGUILayout.LabelField("검색 조건", EditorStyles.boldLabel);
            searchBy = (SearchBy)EditorGUILayout.EnumPopup("Search By", searchBy);
            if (searchBy == SearchBy.Name)
            {
                nameMatch = (NameMatch)EditorGUILayout.EnumPopup("Name Match", nameMatch);
                nameQuery = EditorGUILayout.TextField("Name", nameQuery);
                caseSensitive = EditorGUILayout.Toggle("Case Sensitive", caseSensitive);
            }
            else
            {
                tag = EditorGUILayout.TagField("Tag", tag);
            }
            scope = (Scope)EditorGUILayout.EnumPopup("Scope", scope);
            includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("교체 대상 Prefab", EditorStyles.boldLabel);
            prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            if (prefab != null && !IsValidPrefab())
                EditorGUILayout.HelpBox("프로젝트의 Prefab 에셋이 아닙니다. Project 창의 prefab을 넣어주세요.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
            positionOffset = EditorGUILayout.Vector3Field("Position Offset (world)", positionOffset);
            rotMode = (RotMode)EditorGUILayout.EnumPopup("Rotation", rotMode);
            if (rotMode == RotMode.SetEuler || rotMode == RotMode.AddEuler)
                rotEuler = EditorGUILayout.Vector3Field("    Euler", rotEuler);
            scaleMode = (ScaleMode)EditorGUILayout.EnumPopup("Scale", scaleMode);
            if (scaleMode == ScaleMode.SetScale || scaleMode == ScaleMode.MultiplyScale)
                scaleValue = EditorGUILayout.Vector3Field("    Scale", scaleValue);
            keepName = EditorGUILayout.Toggle("Keep Original Name", keepName);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find Matches", GUILayout.Height(26))) FindMatches();
                using (new EditorGUI.DisabledScope(matches.Count == 0 || !IsValidPrefab()))
                    if (GUILayout.Button($"Replace All ({matches.Count})", GUILayout.Height(26))) ReplaceAll();
            }

            if (matches.Count > 0)
            {
                EditorGUILayout.LabelField($"미리보기 ({matches.Count}개)", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(180));
                foreach (var m in matches.Take(300))
                    if (m != null)
                        EditorGUILayout.ObjectField(m, typeof(GameObject), true);
                EditorGUILayout.EndScrollView();
            }
        }

        private bool IsValidPrefab() =>
            prefab != null && EditorUtility.IsPersistent(prefab) &&
            PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.NotAPrefab;

        private void FindMatches()
        {
            matches.Clear();
            var found = new HashSet<GameObject>();
            foreach (var go in GetPool())
                if (go != null && IsMatch(go)) found.Add(go);

            // Replace only top-most matches: drop any match nested under another match
            // (replacing a parent would otherwise destroy its just-replaced children).
            foreach (var go in found)
                if (!HasMatchedAncestor(go, found))
                    matches.Add(go);

            Repaint();
        }

        private IEnumerable<GameObject> GetPool()
        {
            switch (scope)
            {
                case Scope.Selection:
                    return Selection.gameObjects;
                case Scope.SelectionAndChildren:
                    return Selection.gameObjects
                        .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                        .Select(t => t.gameObject);
                default:
                    var active = SceneManager.GetActiveScene();
                    return Object.FindObjectsByType<Transform>(
                            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .Where(t => t.gameObject.scene == active)
                        .Select(t => t.gameObject);
            }
        }

        private bool IsMatch(GameObject go)
        {
            if (!includeInactive && !go.activeInHierarchy) return false;

            if (searchBy == SearchBy.Tag)
            {
                try { return go.CompareTag(tag); }
                catch { return false; }
            }

            if (string.IsNullOrEmpty(nameQuery)) return false;
            string a = go.name, b = nameQuery;
            if (!caseSensitive) { a = a.ToLowerInvariant(); b = b.ToLowerInvariant(); }
            return nameMatch switch
            {
                NameMatch.Exact => a == b,
                NameMatch.StartsWith => a.StartsWith(b),
                NameMatch.EndsWith => a.EndsWith(b),
                _ => a.Contains(b),
            };
        }

        private static bool HasMatchedAncestor(GameObject go, HashSet<GameObject> set)
        {
            var p = go.transform.parent;
            while (p != null)
            {
                if (set.Contains(p.gameObject)) return true;
                p = p.parent;
            }
            return false;
        }

        private void ReplaceAll()
        {
            if (!IsValidPrefab()) return;
            var targets = matches.Where(m => m != null).ToList();
            if (targets.Count == 0) return;

            if (!EditorUtility.DisplayDialog("일괄 교체",
                    $"{targets.Count}개 오브젝트를 '{prefab.name}' (으)로 교체합니다.\n되돌리기(Ctrl+Z) 가능. 계속할까요?",
                    "교체", "취소"))
                return;

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Batch replace prefabs");

            int done = 0;
            foreach (var old in targets)
            {
                if (old == null) continue; // already destroyed as a nested child
                var ot = old.transform;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.scene);
                if (inst == null) continue;
                Undo.RegisterCreatedObjectUndo(inst, "Replace Prefab");

                var t = inst.transform;
                t.SetParent(ot.parent, false);
                t.SetSiblingIndex(ot.GetSiblingIndex());
                t.position = ot.position + positionOffset;
                t.rotation = GetRotation(ot);
                t.localScale = GetScale(ot);
                if (keepName) inst.name = old.name;

                Undo.DestroyObjectImmediate(old);
                done++;
            }

            Undo.CollapseUndoOperations(group);
            matches.Clear();
            Repaint();
            Debug.Log($"[PrefabReplacer] {done}개 교체 완료. (Ctrl+Z로 되돌리기)");
        }

        private Quaternion GetRotation(Transform old) => rotMode switch
        {
            RotMode.SetEuler => Quaternion.Euler(rotEuler),
            RotMode.AddEuler => old.rotation * Quaternion.Euler(rotEuler),
            RotMode.UsePrefab => prefab.transform.rotation,
            _ => old.rotation,
        };

        private Vector3 GetScale(Transform old) => scaleMode switch
        {
            ScaleMode.SetScale => scaleValue,
            ScaleMode.MultiplyScale => Vector3.Scale(old.localScale, scaleValue),
            ScaleMode.UsePrefab => prefab.transform.localScale,
            _ => old.localScale,
        };
    }
}
