using ExtShiftingApp.Analysis;

namespace ExtShiftingApp.Tests.Analysis;

public class DoneFileParserTests
{
    private const string SeedItem = """
        new HashTable from {
          "parent" => "seed",
          "depth" => 0,
          "seq" => 6,
          "triangulation" => {{0,1,3},{1,3,5},{1,2,5}},
          "critRegions" => {}
        }
        """;

    private const string SeedItemWithCritRegion = """
        new HashTable from {
          "parent" => "seed",
          "depth" => 0,
          "seq" => 6,
          "triangulation" => {{0,1,3},{1,3,5},{1,2,5},{2,5,6},{0,2,6}},
          "critRegions" => {new HashTable from {"innerVertexCount" => 0, "boundaryVertexCount" => 3, "regionShape" => "disk"}}
        }
        """;

    private const string SplitItem = """
        new HashTable from {
          "parent" => "0001",
          "depth" => 1,
          "seq" => 7,
          "splitFrom" => new HashTable from {"vertex" => 6, "neighbors" => {1,3}},
          "triangulation" => {{0,1,3},{1,3,5},{1,2,5}},
          "critRegions" => {}
        }
        """;

    private const string MultipleCritRegions = """
        new HashTable from {
          "parent" => "seed",
          "depth" => 0,
          "seq" => 13,
          "triangulation" => {{0,1,3},{1,3,5},{1,2,6},{1,5,6},{0,2,3},{2,3,6},{3,4,5},{4,5,7},{5,6,8},{5,7,8},{3,4,8},{3,6,8},{0,1,4},{1,4,7},{1,2,7},{2,7,8},{0,2,4},{2,4,8}},
          "critRegions" => {new HashTable from {"innerVertexCount" => 1, "boundaryVertexCount" => 4, "regionShape" => "disk"},new HashTable from {"innerVertexCount" => 0, "boundaryVertexCount" => 3, "regionShape" => "disk"}}
        }
        """;

    [Fact]
    public void Parse_SeedItem_AllFieldsCorrect()
    {
        var item = DoneFileParser.Parse(SeedItem);

        Assert.Equal(6, item.Seq);
        Assert.Equal("seed", item.Parent);
        Assert.Equal(0, item.Depth);
        Assert.Equal("{{0,1,3},{1,3,5},{1,2,5}}", item.Triangulation);
        Assert.Empty(item.CritRegions);
        Assert.Null(item.SplitFrom);
    }

    [Fact]
    public void Parse_SeedItem_NullSplitFrom()
    {
        var item = DoneFileParser.Parse(SeedItem);
        Assert.Null(item.SplitFrom);
    }

    [Fact]
    public void Parse_SplitItem_SplitFromPopulated()
    {
        var item = DoneFileParser.Parse(SplitItem);

        Assert.NotNull(item.SplitFrom);
        Assert.Equal(6, item.SplitFrom.Vertex);
        Assert.Equal("{1,3}", item.SplitFrom.Neighbors);
    }

    [Fact]
    public void Parse_OneCritRegion_FieldsCorrect()
    {
        var item = DoneFileParser.Parse(SeedItemWithCritRegion);

        Assert.Single(item.CritRegions);
        var r = item.CritRegions[0];
        Assert.Equal("disk", r.RegionShape);
        Assert.Equal(3, r.BoundaryVertexCount);
        Assert.Equal(0, r.InnerVertexCount);
    }

    [Fact]
    public void Parse_MultipleCritRegions_ListLengthAndValues()
    {
        var item = DoneFileParser.Parse(MultipleCritRegions);

        Assert.Equal(2, item.CritRegions.Count);
        Assert.Equal("disk", item.CritRegions[0].RegionShape);
        Assert.Equal(4, item.CritRegions[0].BoundaryVertexCount);
        Assert.Equal(1, item.CritRegions[0].InnerVertexCount);
        Assert.Equal("disk", item.CritRegions[1].RegionShape);
        Assert.Equal(3, item.CritRegions[1].BoundaryVertexCount);
        Assert.Equal(0, item.CritRegions[1].InnerVertexCount);
    }

    [Fact]
    public void Parse_VertexCount_DistinctVertices()
    {
        // {{0,1,3},{1,3,5},{1,2,5}} has vertices {0,1,2,3,5} = 5 distinct
        var item = DoneFileParser.Parse(SeedItem);
        Assert.Equal(5, item.VertexCount);
    }

    [Fact]
    public void Parse_VertexCount_LargerTriangulation()
    {
        // MultipleCritRegions has vertices 0-8 = 9 distinct
        var item = DoneFileParser.Parse(MultipleCritRegions);
        Assert.Equal(9, item.VertexCount);
    }
}
