namespace VRClassroom.Builders
{
    /// <summary>
    /// 빌드 파이프라인의 한 단계. 새 종류의 오브젝트(예: 창문, 조명 프리팹 등)를
    /// 추가하려면 이 인터페이스를 구현해 ClassroomBuilder 의 빌더 목록에 넣기만 하면 된다.
    /// </summary>
    public interface IElementBuilder
    {
        /// <summary>UI/로그 표시용 이름.</summary>
        string Name { get; }

        /// <summary>주어진 컨텍스트로 해당 종류의 오브젝트를 생성한다.</summary>
        void Build(ClassroomBuildContext ctx);
    }
}
