using ArtificialLife.Core;
using Xunit;

public sealed class ApiTests
{
    [Fact]
    public void PublicSurfaceMatchesTheReviewedContract()
        => Assert.Equal(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "PublicApi.txt")), ApiContract.Describe(typeof(LifeWorld).Assembly));
}
