using ArtificialLife.Core;

var world = new LifeWorld(2026);
world.SetBrain(new UtilityBrain(new IUtilityAction[]
{
    new SatisfyNeedAction("eat", "hunger", 0.4),
    new SatisfyNeedAction("rest", "energy", 0.35),
    new IdleAction(),
}));

var ana = Agent.Create(world.Ids.Next("agent"), "Ana");
ana.Needs.Add("hunger", 0.25, 0.04);
ana.Needs.Add("energy", 0.7, 0.03);
ana.Needs.Add("safety", 1, 0.005);
world.AddAgent(ana);
world.Economy.Open(ana.Id, new Money(10_000));

for (int i = 0; i < 8; i++) world.Step(1);
Console.WriteLine($"tick={world.Tick} action={ana.CurrentAction} hunger={ana.Needs["hunger"].Value:0.00} fingerprint={world.Fingerprint():X16}");

