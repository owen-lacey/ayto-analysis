using System.Reflection;
using System.Text.Json;
using AreYouTheOne.Entities;

namespace AreYouTheOne.Console.Data;

public class RealLife
{
    public static SeasonData LoadEvents(int season)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"AreYouTheOne.data.S{season}.json");
        if (stream == null)
            throw new FileNotFoundException("Could not find embedded resource S1.json");

        using var reader = new StreamReader(stream);
        var jsonContent = reader.ReadToEnd();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var seasonData = JsonSerializer.Deserialize<Entities.SeasonData>(jsonContent, options);
        return seasonData ?? throw new InvalidOperationException("Could not deserialize season data");
    }
}