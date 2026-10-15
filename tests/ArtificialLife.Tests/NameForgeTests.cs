using ArtificialLife.Core;
using Xunit;

public sealed class NameForgeTests
{
    [Fact]
    public void EveryCultureMakesManyUniqueReadableNames()
    {
        var random = new DeterministicRandom(11);
        string[] blocked = { "vovo", "vivo", "magra", "merda", "puta" };
        foreach (NameCulture culture in Enum.GetValues(typeof(NameCulture)))
        {
            var forge = new NameForge();
            var names = Enumerable.Range(0, 5000).Select(i => forge.Person(culture, i % 2 == 0, ref random)).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.DoesNotContain(names, n => n.StartsWith("Sem Nome"));
            Assert.DoesNotContain(names, n => blocked.Any(b => n.Split(' ')[0].Equals(b, StringComparison.OrdinalIgnoreCase)));
            Assert.True(NameForge.Combinations(culture) > 1_000_000, $"{culture} must offer more combinations than the old syllable names.");
        }
    }

    [Fact]
    public void TitlesAreGenderedAndNeverRepeatedInAWorld()
    {
        var random = new DeterministicRandom(3); var forge = new NameForge();
        var titles = Enumerable.Range(0, 30).Select(i => forge.Title(i % 2 == 0, ref random)).Where(t => t != "").ToList();
        Assert.Equal(titles.Count, titles.Distinct().Count());
        Assert.True(titles.Count >= 25);
    }

    [Fact]
    public void PlacesMatchTheirKind()
    {
        var random = new DeterministicRandom(5); var forge = new NameForge();
        Assert.StartsWith("Porto ", forge.Place("Port", NameCulture.Tide, ref random));
        Assert.StartsWith("Mina de ", forge.Place("Mine", NameCulture.Free, ref random));
        Assert.StartsWith("Ninho de ", forge.Place("Nest", NameCulture.Veil, ref random));
        var towns = Enumerable.Range(0, 200).Select(_ => forge.Place("Town", NameCulture.Iron, ref random)).ToList();
        Assert.Equal(towns.Count, towns.Distinct().Count());
    }
}
