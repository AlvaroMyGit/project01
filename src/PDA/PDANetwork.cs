// PDANetwork.cs — Central event bus for PDA news
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.PDA;

/// <summary>
/// Subscribes to EventBus events, converts them into templated PDA messages,
/// and propagates rumors to registered NPC blackboards (spec §3D).
/// </summary>
public sealed class PDANetwork
{
    private readonly List<PDAMessage> _feed = new();
    public IReadOnlyList<PDAMessage> Feed => _feed;

    private readonly List<NPCBlackboard> _listeners = new();
    private StaticWorldGenerator? _worldGen;

    public int MaxFeedSize { get; set; } = 200;
    public float DeathThreatDelta { get; set; } = 15f;
    public float BlowoutThreatDelta { get; set; } = 30f;
    public float MutantEncounterThreatDelta { get; set; } = 12f;

    private static Dictionary<CulturalBackground, string[]> SlangGreetings = new();
    private static Dictionary<CulturalBackground, string[]> SlangAlerts = new();
    private static Dictionary<string, string[]> ChatterTemplates = new();
    private static bool _slangLoaded;
    private static bool _templatesLoaded;

    public PDANetwork()
    {
        EventBus.Subscribe<DeathLogEvent>(OnDeath);
        EventBus.Subscribe<BlowoutWarningEvent>(OnBlowout);
        EventBus.Subscribe<EmissionPhaseChangedEvent>(OnEmissionPhase);
        EventBus.Subscribe<FactionNewsEvent>(OnFactionNews);
        EventBus.Subscribe<BountyEvent>(OnBounty);
        EventBus.Subscribe<TradeOfferEvent>(OnTradeOffer);
        EventBus.Subscribe<MutantEncounterEvent>(OnMutantEncounter);
        EventBus.Subscribe<TreasonCaughtEvent>(OnTreasonCaught);
        EventBus.Subscribe<DisguiseBlownEvent>(OnDisguiseBlown);
    }

    public void BindWorld(StaticWorldGenerator worldGen) => _worldGen = worldGen;

    public static void EnsureSlangLoaded()
    {
        if (_slangLoaded) return;
        string path = Path.Combine("data", "slang.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            void LoadDict(string key, Dictionary<CulturalBackground, string[]> target)
            {
                if (!doc.RootElement.TryGetProperty(key, out var dictElement)) return;
                foreach (var prop in dictElement.EnumerateObject())
                {
                    if (!Enum.TryParse<CulturalBackground>(prop.Name, out var cb)) continue;
                    var list = prop.Value.EnumerateArray()
                        .Select(item => item.GetString() ?? "")
                        .Where(s => s.Length > 0)
                        .ToArray();
                    target[cb] = list;
                }
            }

            LoadDict("SlangGreetings", SlangGreetings);
            LoadDict("SlangAlerts", SlangAlerts);
        }
        _slangLoaded = true;
    }

    public static void EnsureTemplatesLoaded()
    {
        if (_templatesLoaded) return;
        string path = Path.Combine("data", "pda_chatter_templates.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var lines = prop.Value.EnumerateArray()
                    .Select(item => item.GetString() ?? "")
                    .Where(s => s.Length > 0)
                    .ToArray();
                ChatterTemplates[prop.Name] = lines;
            }
        }
        _templatesLoaded = true;
    }

    public static string FormatTemplate(string category, IReadOnlyDictionary<string, string> values)
    {
        EnsureTemplatesLoaded();
        if (!ChatterTemplates.TryGetValue(category, out var lines) || lines.Length == 0)
            return "";

        string template = lines[Random.Shared.Next(lines.Length)];
        foreach (var (key, val) in values)
            template = template.Replace($"{{{key}}}", val, StringComparison.Ordinal);
        return template;
    }

    public void RegisterListener(NPCBlackboard bb)
    {
        if (!_listeners.Contains(bb))
            _listeners.Add(bb);
    }

    public void UnregisterListener(NPCBlackboard bb) => _listeners.Remove(bb);

    public void Post(PDAMessage msg)
    {
        _feed.Add(Normalize(msg));
        if (_feed.Count > MaxFeedSize)
            _feed.RemoveAt(0);
    }

    /// <summary>Resolve a human-readable location label for template placeholders.</summary>
    public string ResolveLocationLabel(string? regionId, float latitude = 0f)
    {
        if (_worldGen != null && !string.IsNullOrEmpty(regionId))
        {
            var region = _worldGen.GetRegionById(regionId);
            if (region != null && !string.IsNullOrEmpty(region.Name))
                return region.Name;
        }
        return ZoneWorldGenerator.GetBandName(latitude);
    }

    public string BandFromPosition(System.Numerics.Vector3 position)
    {
        if (_worldGen == null) return "MidZone";
        float nx = position.X / _worldGen.Width;
        float ny = position.Z / _worldGen.Height;
        return ZoneWorldGenerator.GetBandName(_worldGen.GetThreatLevel(nx, ny));
    }

    public float LatitudeFromPosition(System.Numerics.Vector3 position)
    {
        if (_worldGen == null || _worldGen.Height <= 0) return 0.5f;
        return Math.Clamp(1f - position.Z / _worldGen.Height, 0f, 1f);
    }

    /// <summary>Idle or combat chatter using template categories.</summary>
    public void BroadcastChatter(
        string senderName,
        string senderFaction,
        CulturalBackground culture,
        bool isAlert = false,
        string? regionId = null,
        System.Numerics.Vector3? position = null,
        string? mutantType = null,
        string? weaponName = null)
    {
        EnsureSlangLoaded();
        EnsureTemplatesLoaded();

        float latitude = position.HasValue ? LatitudeFromPosition(position.Value) : 0.3f;
        string locationName = ResolveLocationLabel(regionId, latitude);

        string body;
        if (isAlert && !string.IsNullOrEmpty(mutantType))
        {
            body = FormatTemplate("MutantWarning", new Dictionary<string, string>
            {
                ["senderName"] = senderName,
                ["locationName"] = locationName,
                ["mutantType"] = mutantType
            });
        }
        else if (isAlert)
        {
            body = FormatTemplate("CombatAlert", new Dictionary<string, string>
            {
                ["senderName"] = senderName,
                ["locationName"] = locationName
            });
        }
        else
        {
            body = FormatTemplate("General", new Dictionary<string, string>
            {
                ["senderName"] = senderName,
                ["locationName"] = locationName,
                ["weaponName"] = weaponName ?? "supplies"
            });
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            if (SlangAlerts.TryGetValue(culture, out var alerts) && isAlert && alerts.Length > 0)
                body = alerts[Random.Shared.Next(alerts.Length)];
            else if (SlangGreetings.TryGetValue(culture, out var greetings) && greetings.Length > 0)
                body = greetings[Random.Shared.Next(greetings.Length)];
            else
                body = isAlert ? "Contact! Something's moving!" : "Yeah, copy that.";
        }

        Post(new PDAMessage
        {
            MessageType = isAlert ? PDAMessageType.RumorAlert : PDAMessageType.FactionNews,
            Headline = $"{senderName} ({senderFaction})",
            Body = body,
            FactionId = senderFaction,
            IsUrgent = isAlert
        });
    }

    private void PropagateRumor(string locationTag, float delta)
    {
        foreach (var bb in _listeners)
        {
            bb.LocationThreatMemory.TryGetValue(locationTag, out float cur);
            bb.LocationThreatMemory[locationTag] = cur + delta;
        }
    }

    private static PDAMessage Normalize(PDAMessage msg)
    {
        var type = msg.MessageType != default ? msg.MessageType : msg.Type;
        string id = !string.IsNullOrEmpty(msg.MessageId) ? msg.MessageId : msg.Id;
        if (string.IsNullOrEmpty(id))
            id = Guid.NewGuid().ToString()[..8];

        return new PDAMessage
        {
            MessageId = id,
            Id = id,
            MessageType = type,
            Type = type,
            Headline = msg.Headline,
            Body = msg.Body,
            GameTime = msg.GameTime,
            Latitude = msg.Latitude,
            FactionId = msg.FactionId,
            IsUrgent = msg.IsUrgent
        };
    }

    private void OnDeath(DeathLogEvent e)
    {
        string band = ZoneWorldGenerator.GetBandName(e.Latitude);
        string body = FormatTemplate("DeathReport", new Dictionary<string, string>
        {
            ["victimName"] = e.VictimName,
            ["victimFaction"] = e.FactionId,
            ["locationName"] = band,
            ["mutantType"] = e.KillerName,
            ["killerName"] = e.KillerName,
            ["weaponName"] = e.KillerName
        });
        if (string.IsNullOrWhiteSpace(body))
            body = $"{e.VictimName} was killed by {e.KillerName} in {band}.";

        Post(new PDAMessage
        {
            MessageType = PDAMessageType.DeathLog,
            Headline = $"Death reported — {e.VictimName}",
            Body = body,
            FactionId = e.FactionId,
            Latitude = e.Latitude
        });

        PropagateRumor(band, DeathThreatDelta);
    }

    private void OnBlowout(BlowoutWarningEvent e)
    {
        string body = FormatTemplate("BlowoutWarning", new Dictionary<string, string>
        {
            ["secondsRemaining"] = $"{e.SecondsUntilHit:F0}",
            ["locationName"] = "any macro base"
        });
        if (string.IsNullOrWhiteSpace(body))
            body = $"Emission in {e.SecondsUntilHit:F0} seconds. Seek shelter immediately.";

        Post(new PDAMessage
        {
            MessageType = PDAMessageType.BlowoutWarning,
            Headline = $"EMISSION WARNING — {e.SecondsUntilHit:F0}s",
            Body = body,
            IsUrgent = true
        });

        foreach (var bandName in new[] { "South", "MidZone", "DeepWild", "North" })
            PropagateRumor(bandName, BlowoutThreatDelta);
    }

    private void OnEmissionPhase(EmissionPhaseChangedEvent e)
    {
        if (e.Phase is not (EmissionPhase.Panic or EmissionPhase.Peak or EmissionPhase.Aftermath))
            return;

        string body = FormatTemplate("EmissionActive", new Dictionary<string, string>
        {
            ["phaseName"] = e.Phase.ToString(),
            ["locationName"] = "the Zone"
        });
        if (string.IsNullOrWhiteSpace(body))
            body = $"Emission {e.Phase} phase active. Stay underground.";

        Post(new PDAMessage
        {
            MessageType = PDAMessageType.BlowoutWarning,
            Headline = e.Phase == EmissionPhase.Peak
                ? "EMISSION AT PEAK — STAY UNDERGROUND"
                : $"EMISSION — {e.Phase.ToString().ToUpperInvariant()} PHASE",
            Body = body,
            IsUrgent = true,
            GameTime = e.GameTime
        });

        if (e.Phase is EmissionPhase.Panic or EmissionPhase.Peak)
        {
            foreach (var bandName in new[] { "South", "MidZone", "DeepWild", "North" })
                PropagateRumor(bandName, BlowoutThreatDelta);
        }
    }

    private void OnFactionNews(FactionNewsEvent e) =>
        Post(new PDAMessage
        {
            MessageType = PDAMessageType.FactionNews,
            Headline = e.Headline,
            FactionId = e.FactionId
        });

    private void OnBounty(BountyEvent e) =>
        Post(new PDAMessage
        {
            MessageType = PDAMessageType.Bounty,
            Headline = $"BOUNTY: {e.TargetName} — {e.Reward:F0} RU",
            Body = e.Description
        });

    private void OnTradeOffer(TradeOfferEvent e) =>
        Post(new PDAMessage
        {
            MessageType = PDAMessageType.TradeOffer,
            Headline = $"TRADE: {e.ItemId} from {e.SellerId} — {e.Price:F0} RU"
        });

    private void OnMutantEncounter(MutantEncounterEvent e)
    {
        string body = FormatTemplate("MutantWarning", new Dictionary<string, string>
        {
            ["senderName"] = "Scout",
            ["locationName"] = e.LocationTag,
            ["mutantType"] = e.MutantSpecies
        });
        if (string.IsNullOrWhiteSpace(body))
            body = $"{e.MutantSpecies} activity reported in {e.LocationTag}.";

        Post(new PDAMessage
        {
            MessageType = PDAMessageType.RumorAlert,
            Headline = $"DANGER: {e.MutantSpecies} in {e.LocationTag}",
            Body = body,
            Latitude = e.Latitude,
            IsUrgent = true
        });

        PropagateRumor(e.LocationTag, e.ThreatDelta > 0 ? e.ThreatDelta : MutantEncounterThreatDelta);
    }

    private void OnTreasonCaught(TreasonCaughtEvent e)
    {
        string body = FormatTemplate("TreasonReport", new Dictionary<string, string>
        {
            ["traitorId"] = e.TraitorId,
            ["victimId"] = e.VictimId,
            ["traitorFaction"] = e.TraitorFaction,
            ["locationName"] = "the Zone"
        });
        if (string.IsNullOrWhiteSpace(body))
            body = $"{e.TraitorId} killed squad mate {e.VictimId}.";

        Post(new PDAMessage
        {
            MessageType = PDAMessageType.RumorAlert,
            Headline = $"TREASON — {e.TraitorFaction} stalker turned on a squad mate",
            Body = body,
            FactionId = e.TraitorFaction,
            IsUrgent = true
        });

        PropagateRumor("MidZone", DeathThreatDelta);
    }

    private void OnDisguiseBlown(DisguiseBlownEvent e)
    {
        string locationName = ZoneWorldGenerator.GetBandName(e.Latitude);
        string body = FormatTemplate("DisguiseBlown", new Dictionary<string, string>
        {
            ["trueFaction"] = e.TrueFaction,
            ["disguiseFaction"] = e.DisguiseFaction,
            ["locationName"] = locationName
        });
        if (string.IsNullOrWhiteSpace(body))
            body = $"{e.TrueFaction} infiltrator exposed wearing {e.DisguiseFaction} gear.";

        Post(new PDAMessage
        {
            MessageType = PDAMessageType.RumorAlert,
            Headline = $"DISGUISE BLOWN — {e.TrueFaction} infiltrator exposed",
            Body = body,
            Latitude = e.Latitude,
            IsUrgent = true
        });

        PropagateRumor(locationName, DeathThreatDelta * 0.5f);
    }
}
