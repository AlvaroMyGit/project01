using System.Text;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Factions;

namespace StalkerALifeSandbox.UI.PDAInterface;

/// <summary>
/// Player-facing PDA screen with tabs for Map, Live Feed,
/// Active Bounties, and Faction Status.
/// </summary>
public sealed class PDAInterfacePanel
{
    public bool   IsOpen    { get; set; }
    public string ActiveTab { get; set; } = "Feed"; // Feed, Bounties, Status

    public string Render(PDANetwork network, FactionMatrix factions, int maxMessages = 15)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔══════════════════════════════════════════════════════╗");
        sb.AppendLine("║                STALKER PDA OS v3.0                   ║");
        sb.AppendLine($"║ Active Tab: {ActiveTab,-40} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════╣");

        if (ActiveTab == "Feed")
        {
            RenderFeed(sb, network, maxMessages);
        }
        else if (ActiveTab == "Bounties")
        {
            RenderBounties(sb, network, maxMessages);
        }
        else if (ActiveTab == "Status")
        {
            sb.AppendLine("║ Faction Map Status & Diplomatic Relations            ║");
            sb.AppendLine("║                                                      ║");
            
            string table = factions.DumpTable();
            foreach (var line in table.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // Truncate or pad to fit within the 52-character screen width roughly, or just append
                sb.AppendLine($"║ {line.PadRight(52).Substring(0, 52)} ║");
            }
        }

        sb.AppendLine("╚══════════════════════════════════════════════════════╝");
        return sb.ToString();
    }

    private void RenderFeed(StringBuilder sb, PDANetwork network, int maxMessages)
    {
        int count = network.Feed.Count;
        if (count == 0)
        {
            sb.AppendLine("║  [NO SIGNAL] Awaiting connection to network...       ║");
            return;
        }
        int startIndex = Math.Max(0, count - maxMessages);
        for (int i = startIndex; i < count; i++)
        {
            var msg = network.Feed[i];
            string tag = msg.MessageType switch
            {
                PDAMessageType.BlowoutWarning => "[!EMISSION!]",
                PDAMessageType.DeathLog => "[DEATH]",
                PDAMessageType.DeathReport => "[REPORT]",
                PDAMessageType.Bounty => "[BOUNTY]",
                PDAMessageType.TradeOffer => "[TRADE]",
                PDAMessageType.RumorAlert => "[RUMOR]",
                PDAMessageType.MissionBrief => "[MISSION]",
                PDAMessageType.FactionNews => "[CHATTER]",
                _ => "[INFO]"
            };
            sb.AppendLine($"║ [{msg.GameTime:F1}h] {tag} {msg.Headline}");
            if (!string.IsNullOrEmpty(msg.Body))
                sb.AppendLine($"║    >> {msg.Body}");
        }
    }

    private void RenderBounties(StringBuilder sb, PDANetwork network, int maxMessages)
    {
        var bounties = network.Feed.Where(m => m.MessageType == PDAMessageType.Bounty).ToList();
        if (bounties.Count == 0)
        {
            sb.AppendLine("║  No active bounties posted.                          ║");
            return;
        }
        foreach (var msg in bounties.TakeLast(maxMessages))
        {
            sb.AppendLine($"║ TARGET: {msg.Headline}");
            sb.AppendLine($"║    >> {msg.Body}");
        }
    }
}
