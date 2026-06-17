using UnityEngine;

namespace VRClassroom
{
    /// <summary>
    /// 생성된 모든 오브젝트에 붙는 메타데이터 컴포넌트.
    /// 런타임에 "이 오브젝트가 무엇인지"(원본 id / type / 카테고리)를
    /// 코드로 조회할 수 있게 해 준다. 상호작용/하이라이트 등 확장에 유용.
    /// </summary>
    public class ClassroomElement : MonoBehaviour
    {
        public string sourceId;
        public string sourceType;
        public string label;
        public string category;   // floor / wall / ceiling / door / item

        public void Init(string id, string type, string label, string category)
        {
            this.sourceId = id;
            this.sourceType = type;
            this.label = label;
            this.category = category;
        }
    }
}
