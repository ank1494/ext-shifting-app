namespace ExtShiftingApp.Analysis;

public record CritRegion(string RegionShape, int BoundaryVertexCount, int InnerVertexCount);

public record SplitFromData(int Vertex, string Neighbors);

public record DoneItem(
    int Seq,
    string Parent,
    int Depth,
    string Triangulation,
    int VertexCount,
    List<CritRegion> CritRegions,
    SplitFromData? SplitFrom
);
