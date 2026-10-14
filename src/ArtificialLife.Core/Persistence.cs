using System.Security.Cryptography;
using System.Text;

namespace ArtificialLife.Core;

/// <summary>A versioned, JSON-serializable envelope. Payload is owned by the snapshot, never by the live simulation.</summary>
public sealed class SimulationSave
{
    public int Schema { get; set; } = 1;
    public string Kind { get; set; } = "";
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public string Checksum { get; set; } = "";
}

public sealed record ExtensionState(string Kind, byte[] Payload);
public interface IPersistentUtilityAction : IUtilityAction { ExtensionState CaptureState(); }
public interface IPersistentSimulationCommand : ISimulationCommand { ExtensionState CaptureState(); }

/// <summary>Game-owned implementations must supply codecs for their own action/command state.</summary>
public sealed class RestoreHandlers
{
    public Func<ExtensionState, IUtilityAction>? Action { get; init; }
    public Func<ExtensionState, ISimulationCommand>? Command { get; init; }
    public Func<ExtensionState, ISettlementExtension>? SettlementPolicy { get; init; }
}

internal static class SaveFormat
{
    internal static SimulationSave Capture(string kind, Action<StateWriter> write, int schema = 1)
    {
        using var stream = new MemoryStream();
        using (var writer = new StateWriter(stream)) write(writer);
        byte[] data = stream.ToArray();
        return new() { Schema = schema, Kind = kind, Payload = data, Checksum = Hash(data) };
    }

    internal static T Restore<T>(SimulationSave save, string kind, Func<StateReader, T> read, int maximumSchema = 1)
    {
        if (save is null || save.Schema < 1 || save.Schema > maximumSchema || save.Kind != kind || save.Payload is null ||
            save.Payload.Length > 128 * 1024 * 1024 || save.Checksum != Hash(save.Payload))
            throw new ArgumentException("Invalid snapshot envelope, schema or checksum.", nameof(save));
        try
        {
            using var stream = new MemoryStream(save.Payload, writable: false);
            using var reader = new StateReader(stream);
            T result = read(reader);
            if (stream.Position != stream.Length) throw new ArgumentException("Trailing snapshot data.");
            return result;
        }
        catch (Exception ex) when (ex is IOException or OverflowException or InvalidOperationException or KeyNotFoundException)
        { throw new ArgumentException("Invalid snapshot state.", nameof(save), ex); }
    }

    internal static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}

internal sealed class StateWriter : BinaryWriter
{
    internal StateWriter(Stream stream) : base(stream, Encoding.UTF8, leaveOpen: true) { }
    internal void OptionalId(EntityId? id) { Write(id.HasValue); if (id.HasValue) Write(id.Value.Value); }
    internal void Goods(Inventory inventory)
    {
        var goods = inventory.All.ToArray(); Write(goods.Length);
        foreach (var pair in goods) { Write(pair.Key); Write(pair.Value); }
    }
    internal void Extension(ExtensionState state)
    { Write(state.Kind); Write(state.Payload.Length); Write(state.Payload); }
}

internal sealed class StateReader : BinaryReader
{
    internal StateReader(Stream stream) : base(stream, Encoding.UTF8, leaveOpen: true) { }
    internal int Int(int min = 0, int max = int.MaxValue)
    { int value = ReadInt32(); if (value < min || value > max) throw new ArgumentException("Invalid integer in snapshot."); return value; }
    internal int Count() => Int(0, 1_000_000);
    internal long Long(long min = 0)
    { long value = ReadInt64(); if (value < min) throw new ArgumentException("Invalid counter in snapshot."); return value; }
    internal double Number(double min = 0, double max = double.MaxValue)
    { double value = ReadDouble(); if (!double.IsFinite(value) || value < min || value > max) throw new ArgumentException("Invalid number in snapshot."); return value; }
    internal string Text()
    { string value = ReadString(); if (value.Length > 1_000_000) throw new ArgumentException("Snapshot string too long."); return value; }
    internal string Name()
    { string value = Text(); if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing snapshot identifier."); return value; }
    internal T EnumValue<T>() where T : struct, Enum
    { int value = ReadInt32(); if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentException("Invalid enum in snapshot."); return (T)Enum.ToObject(typeof(T), value); }
    internal EntityId? OptionalId() => ReadBoolean() ? new EntityId(ReadUInt64()) : null;
    internal void Goods(Inventory inventory)
    {
        int count = Count(); var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        { string id = Name(); if (!seen.Add(id)) throw new ArgumentException("Duplicate good."); inventory.Add(id, Long(1)); }
    }
    internal ExtensionState Extension()
    {
        string kind = Name(); int count = Int(0, 16 * 1024 * 1024); byte[] bytes = ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException();
        return new(kind, bytes);
    }
}
