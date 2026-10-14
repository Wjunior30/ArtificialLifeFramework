namespace ArtificialLife.Core;

public sealed partial class EmotionalState
{
    internal void WriteState(StateWriter w)
    { w.Write(_values.Count); foreach (var x in _values.OrderBy(x => x.Key, StringComparer.Ordinal)) { w.Write(x.Key); w.Write(x.Value); } }
    internal void ReadState(StateReader r)
    { int count = r.Count(); for (int i = 0; i < count; i++) _values.Add(r.Name(), r.Number(0, 1)); }
}

public sealed partial class RelationshipSet
{
    internal void WriteState(StateWriter w)
    { w.Write(_opinions.Count); foreach (var x in _opinions.OrderBy(x => x.Key)) { w.Write(x.Key.Value); w.Write(x.Value); } }
    internal void ReadState(StateReader r)
    { int count = r.Count(); for (int i = 0; i < count; i++) _opinions.Add(new(r.ReadUInt64()), r.Number(-1, 1)); }
}

public sealed partial class Agent
{
    internal void WriteState(StateWriter w)
    {
        w.Write(Id.Value); w.Write(Name); w.Write(IsAlive); w.Write(CurrentAction);
        var needs = Needs.All.ToArray(); w.Write(needs.Length);
        foreach (var n in needs) { w.Write(n.Id); w.Write(n.Value); w.Write(n.DecayPerHour); }
        w.Write(Memories.All.Count);
        foreach (var m in Memories.All)
        { w.Write(m.Tick); w.Write(m.Kind); w.Write(m.Description); w.Write(m.Importance); w.OptionalId(m.Subject); }
        Emotions.WriteState(w); Relationships.WriteState(w); w.Goods(Inventory);
    }
    internal static Agent ReadState(StateReader r)
    {
        var agent = Create(new(r.ReadUInt64()), r.Name()); agent.IsAlive = r.ReadBoolean(); agent.CurrentAction = r.Name();
        int needs = r.Count();
        for (int i = 0; i < needs; i++) agent.Needs.Add(r.Name(), r.Number(0, 1), r.Number());
        int memories = r.Int(0, 256);
        for (int i = 0; i < memories; i++) agent.Memories.Remember(new(r.Long(), r.Name(), r.Text(), r.Number(0, 1), r.OptionalId()));
        agent.Emotions.ReadState(r); agent.Relationships.ReadState(r); r.Goods(agent.Inventory);
        return agent;
    }
}

public sealed partial class BodyState
{
    internal void WriteState(StateWriter w)
    { w.Write(Health); w.Write(_injuries.Count); foreach (var x in _injuries.OrderBy(x => x.Key)) { w.Write((int)x.Key); w.Write((int)x.Value); } }
    internal void ReadState(StateReader r)
    { Health = r.Number(0, 1); int count = r.Int(0, 6); for (int i = 0; i < count; i++) _injuries.Add(r.EnumValue<BodyPart>(), r.EnumValue<InjurySeverity>()); }
}

public sealed partial class PersonProfile
{
    internal void WriteState(StateWriter w)
    {
        w.Write(AgentId.Value); w.Write((int)Gender); w.Write((int)Occupation); w.Write(AgeYears);
        w.Write(Position.X); w.Write(Position.Y); w.OptionalId(HouseholdId); w.OptionalId(EmployerId); w.OptionalId(FactionId);
        w.Write(IsCaptive); w.Write(CrimeRisk); Body.WriteState(w);
        w.Write(_skills.Count); foreach (var x in _skills.OrderBy(x => x.Key)) { w.Write((int)x.Key); w.Write(x.Value); }
        w.Write(_traits.Count); foreach (var x in _traits.OrderBy(x => x.Key, StringComparer.Ordinal)) { w.Write(x.Key); w.Write(x.Value); }
        w.Write(_knowledge.Count); foreach (string fact in Knowledge) w.Write(fact);
        w.Write(_goals.Count); foreach (var goal in _goals) { w.Write(goal.Id); w.Write(goal.Priority); w.Write(goal.ExpiresAtTick); }
    }
    internal static PersonProfile ReadState(StateReader r)
    {
        var profile = new PersonProfile(new(r.ReadUInt64()))
        {
            Gender = r.EnumValue<Gender>(), Occupation = r.EnumValue<Occupation>(), AgeYears = r.Int(),
            Position = new(r.Number(-double.MaxValue), r.Number(-double.MaxValue)),
            HouseholdId = r.OptionalId(), EmployerId = r.OptionalId(), FactionId = r.OptionalId(),
            IsCaptive = r.ReadBoolean(), CrimeRisk = r.Number(0, 1)
        };
        profile.Body.ReadState(r);
        int count = r.Count(); for (int i = 0; i < count; i++) profile._skills.Add(r.EnumValue<Skill>(), r.Number(0, 1));
        count = r.Count(); for (int i = 0; i < count; i++) profile._traits.Add(r.Name(), r.Number(-1, 1));
        count = r.Count(); for (int i = 0; i < count; i++) if (!profile._knowledge.Add(r.Name())) throw new ArgumentException("Duplicate knowledge.");
        count = r.Count(); var goals = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        { var goal = new Goal(r.Name(), r.Number(), r.Long()); if (!goals.Add(goal.Id)) throw new ArgumentException("Duplicate goal."); profile._goals.Add(goal); }
        return profile;
    }
}

public sealed partial class Economy
{
    internal void WriteState(StateWriter w)
    {
        Audit(); w.Write(_issuedCents); w.Write(_accounts.Count);
        foreach (var account in Accounts) { w.Write(account.Owner.Value); w.Write(account.Balance.Cents); }
        w.Write(_ledger.Count);
        foreach (var entry in _ledger)
        { w.Write(entry.Tick); w.Write(entry.From.Value); w.Write(entry.To.Value); w.Write(entry.Amount.Cents); w.Write(entry.Reason); }
    }
    internal static Economy ReadState(StateReader r, long tick)
    {
        var economy = new Economy(); long issued = r.Long(); int count = r.Count();
        for (int i = 0; i < count; i++) economy.Open(new(r.ReadUInt64()), new(r.Long()));
        if (economy._issuedCents != issued) throw new ArgumentException("Snapshot money conservation failed.");
        count = r.Count();
        for (int i = 0; i < count; i++)
        {
            var entry = new LedgerEntry(r.Long(), new(r.ReadUInt64()), new(r.ReadUInt64()), new(r.Long(1)), r.Text());
            if (entry.Tick > tick || !economy.HasAccount(entry.From) || !economy.HasAccount(entry.To)) throw new ArgumentException("Invalid ledger entry.");
            economy._ledger.Add(entry);
        }
        economy.Audit(); return economy;
    }
}

public sealed partial class SatisfyNeedAction
{
    internal void WriteState(StateWriter w) { w.Write(Id); w.Write(_need); w.Write(_amount); }
}

public sealed partial class UtilityBrain
{
    internal void WriteState(StateWriter w)
    {
        w.Write(_actions.Length);
        foreach (var action in _actions)
        {
            if (action is IdleAction) w.Write("core.idle.v1");
            else if (action is SatisfyNeedAction need) { w.Write("core.need.v1"); need.WriteState(w); }
            else if (action is IPersistentUtilityAction persistent) { w.Write("extension"); w.Extension(persistent.CaptureState()); }
            else throw new InvalidOperationException($"Action {action.Id} must implement IPersistentUtilityAction to be saved.");
        }
    }
    internal static UtilityBrain ReadState(StateReader r, RestoreHandlers? handlers)
    {
        int count = r.Int(1, 100_000); var actions = new List<IUtilityAction>();
        for (int i = 0; i < count; i++)
        {
            string kind = r.Name();
            actions.Add(kind switch
            {
                "core.idle.v1" => new IdleAction(),
                "core.need.v1" => new SatisfyNeedAction(r.Name(), r.Name(), r.Number(-double.MaxValue)),
                "extension" => handlers?.Action?.Invoke(r.Extension()) ?? throw new ArgumentException("A custom action restore handler is required."),
                _ => throw new ArgumentException("Unknown action schema.")
            });
        }
        return new(actions);
    }
}

public sealed partial class LifeWorld
{
    public SimulationSave CaptureState() => SaveFormat.Capture("life-world", WriteState);
    public static LifeWorld Restore(SimulationSave save, RestoreHandlers? handlers = null) => SaveFormat.Restore(save, "life-world", r => ReadState(r, handlers));
    public string StateHash() => CaptureState().Checksum;

    private LifeWorld(ulong mixedSeed, long tick, ulong nextId, Economy economy)
    { _seed = mixedSeed; Tick = tick; Ids = new(mixedSeed, nextId); Economy = economy; }

    internal void WriteState(StateWriter w)
    {
        w.Write(_seed); w.Write(Tick); w.Write(Ids.NextValue); w.Write(EventCapacity); w.Write(DroppedEvents);
        Economy.WriteState(w); w.Write(_brain is not null); _brain?.WriteState(w);
        w.Write(_agents.Count); foreach (var agent in _agents.Values) agent.WriteState(w);
        w.Write(_events.Count); foreach (var entry in _events)
        { w.Write(entry.Tick); w.Write(entry.Kind); w.Write(entry.Subject.Value); w.Write(entry.Message); }
    }
    internal static LifeWorld ReadState(StateReader r, RestoreHandlers? handlers)
    {
        ulong seed = r.ReadUInt64(); long tick = r.Long(); ulong next = r.ReadUInt64();
        if (next == 0) throw new ArgumentException("Invalid next entity identifier.");
        int capacity = r.Int(0, 1_000_000); long dropped = r.Long();
        var world = new LifeWorld(seed, tick, next, Economy.ReadState(r, tick)) { EventCapacity = capacity, DroppedEvents = dropped };
        if (r.ReadBoolean()) world._brain = UtilityBrain.ReadState(r, handlers);
        int count = r.Count(); for (int i = 0; i < count; i++) world.AddAgent(Agent.ReadState(r));
        count = r.Int(0, capacity);
        for (int i = 0; i < count; i++)
        {
            var entry = new LifeEvent(r.Long(), r.Name(), new(r.ReadUInt64()), r.Text());
            if (entry.Tick > tick) throw new ArgumentException("Future event in snapshot.");
            world._events.Add(entry);
        }
        return world;
    }
}
