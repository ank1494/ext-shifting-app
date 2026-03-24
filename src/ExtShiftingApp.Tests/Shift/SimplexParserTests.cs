using ExtShiftingApp.Shift;

namespace ExtShiftingApp.Tests.Shift;

public class SimplexParserTests
{
    // --- Valid inputs ---

    [Fact]
    public void Parse_ValidEdges_ReturnsSimplices()
    {
        var result = SimplexParser.Parse("{{1,2},{1,3},{3,4}}");

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Simplices!.Count);
        Assert.Contains(result.Simplices, s => s.SequenceEqual([1, 2]));
        Assert.Contains(result.Simplices, s => s.SequenceEqual([1, 3]));
        Assert.Contains(result.Simplices, s => s.SequenceEqual([3, 4]));
    }

    [Fact]
    public void Parse_SingleSimplex_IsValid()
    {
        var result = SimplexParser.Parse("{{1,2}}");

        Assert.True(result.IsValid);
        Assert.Single(result.Simplices!);
    }

    [Fact]
    public void Parse_WithWhitespace_IsValid()
    {
        var result = SimplexParser.Parse("{{ 1, 2 }, { 1, 3 }}");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Simplices!.Count);
    }

    [Fact]
    public void Parse_NonContiguousVertexLabels_IsValid()
    {
        var result = SimplexParser.Parse("{{1,5},{1,10}}");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Simplices!.Count);
    }

    // --- Invalid inputs ---

    [Fact]
    public void Parse_EmptyInput_ReturnsError()
    {
        var result = SimplexParser.Parse("");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_MalformedBraces_ReturnsError()
    {
        var result = SimplexParser.Parse("{1,2},{1,3}");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_NonIntegerVertices_ReturnsError()
    {
        var result = SimplexParser.Parse("{{a,b},{c,d}}");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_NegativeVertices_ReturnsError()
    {
        var result = SimplexParser.Parse("{{-1,2},{-1,3}}");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_InconsistentDimensions_ReturnsError()
    {
        var result = SimplexParser.Parse("{{1,2},{3,4,5}}");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_DuplicateVerticesInSimplex_ReturnsError()
    {
        var result = SimplexParser.Parse("{{1,1,2}}");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_EmptySimplex_ReturnsError()
    {
        var result = SimplexParser.Parse("{{}}");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }
}
