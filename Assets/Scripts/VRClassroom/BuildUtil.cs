using System.Collections.Generic;
using UnityEngine;

namespace VRClassroom
{
    /// <summary>기하/좌표 관련 헬퍼.</summary>
    public static class GeometryUtil
    {
        /// <summary>
        /// Y축 rotation_y(도) 로 회전했을 때, 큐브의 로컬 X축(=가로/길이 방향)이
        /// 월드에서 향하는 단위 벡터. 벽을 따라 문 위치를 계산할 때 사용.
        /// </summary>
        public static Vector3 WidthAxis(float rotationYDeg)
        {
            float r = rotationYDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r));
        }
    }

    /// <summary>URP/Lit 머티리얼을 색상 키 기준으로 캐싱 생성.</summary>
    public class MaterialFactory
    {
        readonly Shader _shader;
        readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public MaterialFactory()
        {
            _shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_shader == null) _shader = Shader.Find("Standard");
            if (_shader == null) _shader = Shader.Find("Sprites/Default");
        }

        public Material GetColored(string key, Color color)
        {
            if (_cache.TryGetValue(key, out var m)) return m;

            m = new Material(_shader) { name = "VRClassroom_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); // URP
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);         // Built-in
            _cache[key] = m;
            return m;
        }
    }

    /// <summary>런타임/에디터 양쪽에서 안전하게 쓰는 오브젝트 헬퍼.</summary>
    public static class BuildObjectUtil
    {
        public static GameObject CreateBox(string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            return go;
        }

        public static void SetCollider(GameObject go, bool enabled, bool isTrigger = false)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            if (!enabled)
            {
                Object.Destroy(col);
                // 에디터(비플레이) 에서는 Destroy 가 즉시 적용되지 않으므로 보조 처리
#if UNITY_EDITOR
                if (!Application.isPlaying) Object.DestroyImmediate(col);
#endif
            }
            else
            {
                col.isTrigger = isTrigger;
            }
        }

        public static void ApplyMaterial(GameObject go, Material mat)
        {
            if (mat == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        /// <summary>
        /// 태그가 프로젝트에 정의되어 있으면 적용, 없으면 경고만 남기고 통과.
        /// (에디터 빌드 경로에서는 미리 TagSetupUtility 로 태그를 생성한다)
        /// </summary>
        public static void SetTagSafe(GameObject go, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            try
            {
                go.tag = tag;
            }
            catch
            {
                Debug.LogWarning($"[VRClassroom] 태그 '{tag}' 가 정의되지 않았습니다. " +
                                 "메뉴 [Tools > VR Classroom > Setup Tags] 를 먼저 실행하세요.");
            }
        }

        public static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Object.DestroyImmediate(obj); return; }
#endif
            Object.Destroy(obj);
        }
    }
}
