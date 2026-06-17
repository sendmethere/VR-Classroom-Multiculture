using TMPro;
using UnityEditor;
using UnityEngine;

namespace AIAvatar.EditorTools
{
    /// <summary>
    /// Korean TMP font support. The stock TMP font (LiberationSans) has no Hangul,
    /// so Korean text renders as boxes. This:
    ///   1) copies a Korean .ttf into the project (Windows Malgun Gothic by default),
    ///   2) builds a DYNAMIC TMP font asset from it, and
    ///   3) registers it in the TMP global Fallback list.
    /// Because it's a global fallback, EVERY TMP text (including ones still set to
    /// LiberationSans) renders Korean — no per-text reassignment needed.
    ///
    /// Run via: Tools ▸ AI Avatar ▸ Setup Korean Font.
    ///
    /// ⚠ Malgun Gothic is a Microsoft system font — fine for local development, but
    ///   for distribution use an openly-licensed Korean font (Noto Sans KR, Nanum
    ///   Gothic): drop its .ttf into Art/Fonts and point TtfPath at it.
    /// </summary>
    public static class AIAvatarFontUtil
    {
        private const string ArtDir = "Assets/AIAvatar/Art";
        private const string FontDir = ArtDir + "/Fonts";
        private const string TtfPath = FontDir + "/KoreanGothic.ttf";
        private const string FontAssetPath = FontDir + "/Korean SDF.asset";

        // First existing OS font wins.
        private static readonly string[] OsFontFiles =
        {
            @"C:\Windows\Fonts\malgun.ttf",
            @"C:\Windows\Fonts\gulim.ttc",
            @"C:\Windows\Fonts\batang.ttc",
        };

        [MenuItem("Tools/AI Avatar/Setup Korean Font", false, 30)]
        public static void SetupKoreanFontMenu()
        {
            var font = GetOrCreateKoreanFont();
            AssetDatabase.Refresh();
            if (font != null)
            {
                Selection.activeObject = font;
                EditorGUIUtility.PingObject(font);
            }
        }

        /// <summary>Ensure the Korean TMP font exists and is a global TMP fallback.</summary>
        public static TMP_FontAsset GetOrCreateKoreanFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null) font = CreateFontAsset();
            if (font != null) RegisterGlobalFallback(font);
            return font;
        }

        private static TMP_FontAsset CreateFontAsset()
        {
            EnsureFolder();

            // 1) Make sure a Korean TTF is in the project (copy from the OS if needed).
            if (!System.IO.File.Exists(TtfPath))
            {
                string src = null;
                foreach (var f in OsFontFiles)
                    if (System.IO.File.Exists(f)) { src = f; break; }
                if (src == null)
                {
                    Debug.LogWarning("[AIAvatar] 한국어 OS 폰트를 찾지 못했어요. 한국어 .ttf를 " + FontDir + " 에 직접 넣고 다시 시도하세요.");
                    return null;
                }
                try { System.IO.File.Copy(src, TtfPath, true); }
                catch (System.Exception e) { Debug.LogWarning($"[AIAvatar] 폰트 복사 실패: {e.Message}"); return null; }
                AssetDatabase.ImportAsset(TtfPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (ttf == null) { Debug.LogWarning("[AIAvatar] TTF 임포트 실패: " + TtfPath); return null; }

            TMP_FontAsset tmp;
            try { tmp = TMP_FontAsset.CreateFontAsset(ttf); } // dynamic atlas (glyphs on demand)
            catch (System.Exception e) { Debug.LogWarning($"[AIAvatar] TMP 폰트 생성 실패: {e.Message}"); return null; }
            if (tmp == null) return null;

            AssetDatabase.CreateAsset(tmp, FontAssetPath);
            if (tmp.atlasTexture != null)
            {
                tmp.atlasTexture.name = "Korean Atlas";
                AssetDatabase.AddObjectToAsset(tmp.atlasTexture, tmp);
            }
            if (tmp.material != null)
            {
                tmp.material.name = "Korean SDF Material";
                AssetDatabase.AddObjectToAsset(tmp.material, tmp);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            Debug.Log("[AIAvatar] 한국어 TMP 폰트 생성: " + FontAssetPath);
            return tmp;
        }

        private static void RegisterGlobalFallback(TMP_FontAsset font)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[AIAvatar] TMP Settings를 찾지 못했어요. Window ▸ TextMeshPro ▸ Import TMP Essential Resources 후 다시 시도하세요.");
                return;
            }
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            if (list == null) { Debug.LogWarning("[AIAvatar] TMP Settings에서 fallback 목록을 찾지 못했어요."); return; }

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == font)
                    return; // already registered

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = font;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[AIAvatar] 한국어 폰트를 TMP 전역 Fallback에 등록했어요 ✅  이제 모든 TMP 텍스트에서 한글이 보입니다.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ArtDir))
                AssetDatabase.CreateFolder("Assets/AIAvatar", "Art");
            if (!AssetDatabase.IsValidFolder(FontDir))
                AssetDatabase.CreateFolder(ArtDir, "Fonts");
        }
    }
}
