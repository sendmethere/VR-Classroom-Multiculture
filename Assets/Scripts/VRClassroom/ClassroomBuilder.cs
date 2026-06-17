using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRClassroom.Builders;
using VRClassroom.Data;

namespace VRClassroom
{
    /// <summary>
    /// JSON 레이아웃을 받아 씬에 교실 공간을 만드는 오케스트레이터.
    /// 빌드 단계는 <see cref="IElementBuilder"/> 목록으로 구성되어 있어
    /// 단계를 추가/교체/재정렬하기 쉽다.
    ///
    /// 사용법
    ///  - 에디터: 인스펙터의 파일 드롭다운에서 JSON 선택 후 [Build] (ClassroomBuilderEditor)
    ///  - 런타임: BuildFromStreamingAssets(fileName) 또는 ClassroomRuntimeMenu 의 파일 선택 UI
    /// </summary>
    [DisallowMultipleComponent]
    public class ClassroomBuilder : MonoBehaviour
    {
        [Header("불러올 JSON")]
        [Tooltip("StreamingAssets 안의 파일 이름")]
        public string streamingAssetsFileName = "classroom_layout_1780279053988.json";

        [Tooltip("비어 있지 않으면 이 절대 경로를 우선 사용 (에디터에서 Browse 로 선택)")]
        public string absolutePathOverride = "";

        [Header("설정")]
        [Tooltip("비워 두면 기본 설정이 자동 적용됨")]
        public ClassroomBuildSettings settings;

        [Header("동작")]
        public bool buildOnStart = false;
        [Tooltip("생성물을 담을 루트(없으면 자동 생성)")]
        public Transform buildRoot;

        const string RootName = "ClassroomRoot";

        void Start()
        {
            if (buildOnStart)
                BuildFromStreamingAssets(streamingAssetsFileName);
        }

        // -------------------------------------------------------------
        // 진입점
        // -------------------------------------------------------------

        /// <summary>런타임용: StreamingAssets 의 파일을 코루틴으로 로드 후 빌드(Android 호환).</summary>
        public void BuildFromStreamingAssets(string fileName)
        {
            string path = ClassroomJsonLoader.StreamingAssetsPath(fileName);
            StartCoroutine(BuildRoutine(path));
        }

        IEnumerator BuildRoutine(string path)
        {
            ClassroomLayout layout = null;
            yield return ClassroomJsonLoader.LoadLayoutRoutine(path, l => layout = l);
            if (layout != null) Build(layout);
        }

        /// <summary>에디터/데스크톱용: 절대 경로에서 동기 로드 후 빌드.</summary>
        public void BuildFromFileSync(string absolutePath)
        {
            var layout = ClassroomJsonLoader.LoadSync(absolutePath);
            if (layout != null) Build(layout);
        }

        /// <summary>인스펙터 설정(absolutePathOverride 우선, 없으면 StreamingAssets)에 따라 동기 빌드.</summary>
        public void BuildFromConfiguredPathSync()
        {
            string path = !string.IsNullOrEmpty(absolutePathOverride)
                ? absolutePathOverride
                : ClassroomJsonLoader.StreamingAssetsPath(streamingAssetsFileName);
            BuildFromFileSync(path);
        }

        // -------------------------------------------------------------
        // 빌드 본체
        // -------------------------------------------------------------

        public void Build(ClassroomLayout layout)
        {
            if (layout == null)
            {
                Debug.LogError("[VRClassroom] 레이아웃이 null 입니다.");
                return;
            }

            Clear();

            var activeSettings = settings != null ? settings : ClassroomBuildSettings.CreateRuntimeDefault();

            Transform root = GetOrCreateRoot();
            var ctx = new ClassroomBuildContext(layout, activeSettings, root);

            foreach (var builder in CreateBuilders())
            {
                try { builder.Build(ctx); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[VRClassroom] '{builder.Name}' 단계 실패: {e}");
                }
            }

            Debug.Log($"[VRClassroom] 빌드 완료 - floors:{Count(layout.floors)} walls:{Count(layout.walls)} " +
                      $"doors:{Count(layout.doors)} items:{Count(layout.items)}");
        }

        /// <summary>
        /// 빌드 파이프라인 정의. 순서가 의미를 가진다(바닥→벽→문→천장→아이템).
        /// 여기에 새 IElementBuilder 를 추가하면 기능이 확장된다.
        /// </summary>
        protected virtual List<IElementBuilder> CreateBuilders()
        {
            return new List<IElementBuilder>
            {
                new FloorBuilder(),
                new WallBuilder(),    // 문 위치를 비워 벽을 만든다(=문 뚫기)
                new DoorBuilder(),    // 문 마커
                new CeilingBuilder(), // 나무바닥(구획된) 공간 위에만 천장
                new ItemBuilder(),
            };
        }

        // -------------------------------------------------------------
        // 루트 관리 / 비우기
        // -------------------------------------------------------------

        Transform GetOrCreateRoot()
        {
            if (buildRoot != null) return buildRoot;

            var existing = transform.Find(RootName);
            if (existing != null) return existing;

            var go = new GameObject(RootName);
            go.transform.SetParent(transform, false);
            buildRoot = go.transform;
            return buildRoot;
        }

        /// <summary>이전 빌드 결과물 제거.</summary>
        public void Clear()
        {
            Transform root = buildRoot != null ? buildRoot : transform.Find(RootName);
            if (root == null) return;

            // 자식 전체 제거
            for (int i = root.childCount - 1; i >= 0; i--)
                BuildObjectUtil.SafeDestroy(root.GetChild(i).gameObject);
        }

        static int Count<T>(List<T> list) => list != null ? list.Count : 0;
    }
}
