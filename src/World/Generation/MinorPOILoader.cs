using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StalkerALifeSandbox.World.Generation;

public static class MinorPOILoader
{
    public static List<MinorPOI> Load(string path)
    {
        if (!File.Exists(path))
            return new List<MinorPOI>();
        string json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(null, false));
        var pois = JsonSerializer.Deserialize<List<MinorPOI>>(json, options);
        return pois ?? new List<MinorPOI>();
    }
}
