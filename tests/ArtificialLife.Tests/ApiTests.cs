using ArtificialLife.Core;
using Xunit;

public sealed class ApiTests
{
    [Fact]
    public void PublicSurfaceMatchesTheReviewedContract()
    {
        var actual = ApiContract.Describe(typeof(LifeWorld).Assembly);
        // Set ALF_UPDATE_API to the repo's docs/PublicApi.txt path to accept an intentional API change.
        if (Environment.GetEnvironmentVariable("ALF_UPDATE_API") is { Length: > 0 } target) File.WriteAllLines(target, actual);
        Assert.Equal(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "PublicApi.txt")), actual);
    }
}
