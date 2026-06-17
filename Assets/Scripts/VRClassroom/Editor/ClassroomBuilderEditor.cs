using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRClassroom.EditorTools
{
    /// <summary>
    /// ClassroomBuilder 의 커스텀 인스펙터.
    /// - StreamingAssets 안의 JSON 파일을 드롭다운으로 선택
    /// - [Browse] 로 임의 위치의 JSON 직접 선택
    /// - [Build] / [Clear] 버튼 (에디터에서 즉시 동기 빌드, 태그 자동 생성)
    /// </summary>
    [CustomEditor(typeof(ClassroomBuilder))]
    public class ClassroomBuilderEditor : Editor
    {
        string[] _jsonFiles;
        int _selectedIndex;

        void OnEnable() => RefreshFiles();

        void RefreshFiles()
        {
            string dir = Application.streamingAssetsPath;
            _jsonFiles = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json").Select(Path.GetFileName).ToArray()
                : new string[0];

            var b = (ClassroomBuilder)target;
            _selectedIndex = System.Array.IndexOf(_jsonFiles, b.streamingAssetsFileName);
            if (_selectedIndex < 0) _selectedIndex = 0;
        }

        public override void OnInspectorGUI()
        {
            var builder = (ClassroomBuilder)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("불러올 파일 선택", EditorStyles.boldLabel);

            // --- StreamingAssets 드롭다운 ---
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_jsonFiles != null && _jsonFiles.Length > 0)
                {
                    int newIndex = EditorGUILayout.Popup("StreamingAssets", _selectedIndex, _jsonFiles);
                    if (newIndex != _selectedIndex)
                    {
                        _selectedIndex = newIndex;
                        Undo.RecordObject(builder, "Select JSON");
                        builder.streamingAssetsFileName = _jsonFiles[_selectedIndex];
                        builder.absolutePathOverride = ""; // 드롭다운 선택 시 override 해제
                        EditorUtility.SetDirty(builder);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("StreamingAssets 에 .json 이 없습니다.", MessageType.Info);
                }

                if (GUILayout.Button("새로고침", GUILayout.Width(70)))
                    RefreshFiles();
            }

            // --- 임의 경로 Browse ---
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("직접 선택", GUILayout.Width(60));
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(builder.absolutePathOverride) ? "(없음)" : builder.absolutePathOverride,
                    EditorStyles.miniLabel);

                if (GUILayout.Button("Browse...", GUILayout.Width(80)))
                {
                    string start = Directory.Exists(Application.streamingAssetsPath)
                        ? Application.streamingAssetsPath : Application.dataPath;
                    string picked = EditorUtility.OpenFilePanel("교실 레이아웃 JSON 선택", start, "json");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        Undo.RecordObject(builder, "Browse JSON");
                        builder.absolutePathOverride = picked;
                        EditorUtility.SetDirty(builder);
                    }
                }
                if (!string.IsNullOrEmpty(builder.absolutePathOverride) &&
                    GUILayout.Button("X", GUILayout.Width(24)))
                {
                    builder.absolutePathOverride = "";
                    EditorUtility.SetDirty(builder);
                }
            }

            EditorGUILayout.Space();

            // --- 액션 버튼 ---
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
                if (GUILayout.Button("Build (씬 생성)", GUILayout.Height(30)))
                {
                    TagSetupUtility.EnsureTagsFromSettings(builder.settings);
                    builder.BuildFromConfiguredPathSync();
                    MarkSceneDirty();
                }
                GUI.backgroundColor = new Color(0.95f, 0.6f, 0.6f);
                if (GUILayout.Button("Clear (비우기)", GUILayout.Height(30)))
                {
                    builder.Clear();
                    MarkSceneDirty();
                }
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button("태그만 미리 생성 (Setup Tags)"))
                TagSetupUtility.EnsureTagsFromSettings(builder.settings);
        }

        static void MarkSceneDirty()
        {
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
