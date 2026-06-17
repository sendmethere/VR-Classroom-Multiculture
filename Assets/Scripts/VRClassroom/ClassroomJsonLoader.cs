using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using VRClassroom.Data;

namespace VRClassroom
{
    /// <summary>
    /// JSON 파일을 읽어 <see cref="ClassroomLayout"/> 로 파싱한다.
    /// - 데스크톱/에디터: File.ReadAllText (동기)
    /// - Android(Quest 등) StreamingAssets: APK 안에 압축되어 있으므로
    ///   반드시 UnityWebRequest 로 비동기 로드해야 한다.
    /// </summary>
    public static class ClassroomJsonLoader
    {
        /// <summary>문자열(JSON) → ClassroomLayout. 실패 시 null.</summary>
        public static ClassroomLayout Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[VRClassroom] JSON 내용이 비어 있습니다.");
                return null;
            }

            try
            {
                LayoutRoot root = JsonUtility.FromJson<LayoutRoot>(json);
                if (root == null || root.classroom_layout == null)
                {
                    Debug.LogError("[VRClassroom] 'classroom_layout' 루트를 찾지 못했습니다. JSON 형식을 확인하세요.");
                    return null;
                }
                return root.classroom_layout;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRClassroom] JSON 파싱 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>로컬 경로에서 동기 로드(에디터/데스크톱 전용).</summary>
        public static ClassroomLayout LoadSync(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[VRClassroom] 파일을 찾을 수 없습니다: {absolutePath}");
                return null;
            }
            return Parse(File.ReadAllText(absolutePath));
        }

        /// <summary>
        /// 플랫폼에 상관없이 안전하게 텍스트를 읽는 코루틴.
        /// StreamingAssets(특히 Android) 처리를 위해 UnityWebRequest 를 사용한다.
        /// </summary>
        public static IEnumerator LoadTextRoutine(string path, Action<string> onText, Action<string> onError = null)
        {
            // file:// 또는 jar:file:// 가 아니면, UnityWebRequest 가 읽을 수 있도록 URI 로 변환
            string uri = path.Contains("://") ? path : "file://" + path;

            using (UnityWebRequest req = UnityWebRequest.Get(uri))
            {
                yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool failed = req.result != UnityWebRequest.Result.Success;
#else
                bool failed = req.isNetworkError || req.isHttpError;
#endif
                if (failed)
                {
                    string msg = $"[VRClassroom] 로드 실패 ({uri}): {req.error}";
                    if (onError != null) onError(msg); else Debug.LogError(msg);
                    yield break;
                }
                onText?.Invoke(req.downloadHandler.text);
            }
        }

        /// <summary>코루틴으로 레이아웃까지 한 번에 로드.</summary>
        public static IEnumerator LoadLayoutRoutine(string path, Action<ClassroomLayout> onLoaded, Action<string> onError = null)
        {
            ClassroomLayout layout = null;
            yield return LoadTextRoutine(path, txt => layout = Parse(txt), onError);
            onLoaded?.Invoke(layout);
        }

        /// <summary>StreamingAssets 안의 파일 이름 → 절대/URI 경로.</summary>
        public static string StreamingAssetsPath(string fileName)
        {
            return Path.Combine(Application.streamingAssetsPath, fileName);
        }
    }
}
