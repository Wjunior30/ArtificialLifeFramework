namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    private void BuildSites()
    {
        for (int index = 0; index < _rules.SiteCount; index++)
        {
            double angle = Math.PI * 2 * index / _rules.SiteCount + Range(-.12, .12);
            double ring = 700 + index % 3 * 620 + Range(-120, 120);
            _sites.Add(new SettlementSite(index, new Vec2(Math.Cos(angle) * ring, Math.Sin(angle) * ring), Range(.35, 1), Range(.25, 1), Range(.2, 1), Range(.25, 1)));
        }
    }

}
