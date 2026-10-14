using ArtificialLife.Core;

string path = "docs/PublicApi.txt";
var lines = ApiContract.Describe(typeof(LifeWorld).Assembly);
if (args.Contains("--check"))
{
    if (!File.Exists(path) || !File.ReadAllLines(path).SequenceEqual(lines)) { Console.Error.WriteLine("Public API differs from the reviewed contract."); return 1; }
}
else { Directory.CreateDirectory("docs"); File.WriteAllLines(path, lines); }
Console.WriteLine($"API contract: {lines.Length} entries.");
return 0;
