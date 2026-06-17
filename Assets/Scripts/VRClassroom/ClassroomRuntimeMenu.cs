using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRClassroom
{
    /// <summary>
    /// 플레이 모드에서 StreamingAssets 안의 JSON 파일을 골라 불러오는 간단한 IMGUI UI.
    /// 캔버스 설정이 필요 없어 바로 동작한다(데스크톱/에디터). 데스크톱에서는 폴더를
    /// 스캔해 목록을 보여주고, Android 등 스캔이 불가한 환경에서는 직접 입력한 파일명을 쓴다.
    /// </summary>
    [RequireComponent(typeof(ClassroomBuilder))]
    public class ClassroomRuntimeMenu : MonoBehaviour
    {
        public bool showMenu = true;
        public KeyCode toggleKey = KeyCode.F1;
        [Tooltip("스캔이 안 되는 플랫폼에서 직접 입력할 파일명")]
        public string manualFileName = "classroom_layout_1780279053988.json";

        ClassroomBuilder _builder;
        List<string> _files;
        Vector2 _scroll;

        void Awake()
        {
            _builder = GetComponent<ClassroomBuilder>();
            RefreshFileList();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) showMenu = !showMenu;
        }

        void RefreshFileList()
        {
            _files = new List<string>();
            try
            {
                if (Directory.Exists(Application.streamingAssetsPath))
                    foreach (var f in Directory.GetFiles(Application.streamingAssetsPath, "*.json"))
                        _files.Add(Path.GetFileName(f));
            }
            catch { /* Android 등: 스캔 불가 → manualFileName 사용 */ }
        }

        void OnGUI()
        {
            if (!showMenu) return;

            const float w = 360f;
            GUILayout.BeginArea(new Rect(10, 10, w, 400), GUI.skin.box);
            GUILayout.Label($"<b>VR Classroom 로더</b>  (토글: {toggleKey})");

            if (GUILayout.Button("파일 목록 새로고침")) RefreshFileList();

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(240));
            if (_files != null && _files.Count > 0)
            {
                foreach (var file in _files)
                    if (GUILayout.Button(file))
                        _builder.BuildFromStreamingAssets(file);
            }
            else
            {
                GUILayout.Label("스캔된 파일이 없습니다. 아래에 파일명을 입력하세요.");
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            manualFileName = GUILayout.TextField(manualFileName);
            if (GUILayout.Button("입력한 파일 불러오기"))
                _builder.BuildFromStreamingAssets(manualFileName);

            if (GUILayout.Button("씬 비우기"))
                _builder.Clear();

            GUILayout.EndArea();
        }
    }
}
