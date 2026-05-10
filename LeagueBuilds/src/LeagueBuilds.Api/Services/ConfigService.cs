using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace LeagueBuilds.Api.Services;

public static class ConfigService
{
    private static string? _cachedApiKey;

    public static async Task<string> GetRiotApiKeyAsync()
    {
        // Return cached key if we already have it (Lambda reuses instances)
        if (_cachedApiKey != null)
            return _cachedApiKey;

        var paramName = Environment.GetEnvironmentVariable("RIOT_API_KEY_PARAM")
            ?? "/league-builds/riot-api-key";

        using var ssmClient = new AmazonSimpleSystemsManagementClient();

        var response = await ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = paramName,
            WithDecryption = true
        });

        _cachedApiKey = response.Parameter.Value;
        return _cachedApiKey;
    }
}