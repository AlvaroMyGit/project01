// PDAMessage.cs — Data structs for news & death logs
namespace StalkerALifeSandbox.PDA;

/// <summary>Category of PDA message.</summary>
public enum PDAMessageType
{
    DeathLog,
    DeathReport,    // Corpse investigation report (identified or unidentified)
    FactionNews,
    BlowoutWarning,
    TradeOffer,
    Bounty,
    RumorAlert,
    MissionBrief
}

/// <summary>
/// A single PDA message stored in the feed.
/// </summary>
public sealed class PDAMessage
{
    public string         MessageId   { get; init; } = "";
    public string         Id          { get; init; } = "";
    public PDAMessageType MessageType { get; init; }
    public PDAMessageType Type        { get; init; }
    public string         Headline    { get; init; } = "";
    public string         Body        { get; init; } = "";
    public float          GameTime    { get; init; }
    public float          Latitude    { get; init; }
    public string?        FactionId   { get; init; }
    public bool           IsUrgent    { get; init; }

    public override string ToString() =>
        $"[PDA:{MessageType}] {Headline}";
}
