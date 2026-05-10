using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeagueBuilds.Api.Models;

namespace LeagueBuilds.Api.Services;

public class RiotApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _region;
    private readonly string _platform;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public RiotApiService(string apiKey, string region = "europe", string platform = "euw1")
    {
        _httpClient = new HttpClient();
        _apiKey = apiKey;
        _region = region;
        _platform = platform;
    }

    // Get current patch version
    public async Task<string> GetCurrentPatchAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<List<string>>(
            "https://ddragon.leagueoflegends.com/api/versions.json"
        );
        return response?.FirstOrDefault() ?? "unknown";
    }

    // Get all champion data from Data Dragon
    public async Task<Dictionary<string, ChampionInfo>> GetChampionDataAsync(string patch)
    {
        var url = $"https://ddragon.leagueoflegends.com/cdn/{patch}/data/en_US/champion.json";
        var response = await _httpClient.GetFromJsonAsync<DataDragonChampionResponse>(url, JsonOptions);
        return response?.Data ?? new();
    }

    // Get item data from Data Dragon
    public async Task<Dictionary<string, ItemInfo>> GetItemDataAsync(string patch)
    {
        var url = $"https://ddragon.leagueoflegends.com/cdn/{patch}/data/en_US/item.json";
        var response = await _httpClient.GetFromJsonAsync<DataDragonItemResponse>(url, JsonOptions);
        return response?.Data ?? new();
    }

    // Get rune data from Data Dragon
    public async Task<List<RuneTree>> GetRuneDataAsync(string patch)
    {
        var url = $"https://ddragon.leagueoflegends.com/cdn/{patch}/data/en_US/runesReforged.json";
        return await _httpClient.GetFromJsonAsync<List<RuneTree>>(url, JsonOptions) ?? new();
    }

    // Get summoner by name
    public async Task<SummonerAccount?> GetSummonerByNameAsync(string gameName, string tagLine)
    {
        var url = $"https://{_region}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<SummonerAccount>(JsonOptions);
    }

    // Get match IDs for a player
    public async Task<List<string>> GetMatchIdsAsync(string puuid, int count = 20, string? type = "ranked")
    {
        var url = $"https://{_region}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?count={count}";
        if (type != null) url += $"&type={type}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new();

        return await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions) ?? new();
    }

    // Get match details
    public async Task<RiotMatchResponse?> GetMatchAsync(string matchId)
    {
        var url = $"https://{_region}.api.riotgames.com/lol/match/v5/matches/{matchId}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<RiotMatchResponse>(JsonOptions);
    }

    // Get high-elo players for data collection
    public async Task<List<LeagueEntry>> GetChallengerPlayersAsync()
    {
        var url = $"https://{_platform}.api.riotgames.com/lol/league/v4/challengerleagues/by-queue/RANKED_SOLO_5x5";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new();

        var league = await response.Content.ReadFromJsonAsync<LeagueList>(JsonOptions);
        return league?.Entries ?? new();
    }

    // Get summoner PUUID by summoner ID
    public async Task<string?> GetPuuidBySummonerIdAsync(string summonerId)
    {
        var url = $"https://{_platform}.api.riotgames.com/lol/summoner/v4/summoners/{summonerId}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var summoner = await response.Content.ReadFromJsonAsync<SummonerResponse>(JsonOptions);
        return summoner?.Puuid;
    }
}

// Data Dragon response models
public class DataDragonChampionResponse
{
    public Dictionary<string, ChampionInfo> Data { get; set; } = new();
}

public class ChampionInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public ImageInfo Image { get; set; } = new();
}

public class DataDragonItemResponse
{
    public Dictionary<string, ItemInfo> Data { get; set; } = new();
}

public class ItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ImageInfo Image { get; set; } = new();
    public GoldInfo Gold { get; set; } = new();
}

public class GoldInfo
{
    public int Total { get; set; }
}

public class ImageInfo
{
    public string Full { get; set; } = string.Empty;
}

public class RuneTree
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<RuneSlot> Slots { get; set; } = new();
}

public class RuneSlot
{
    public List<RuneInfo> Runes { get; set; } = new();
}

public class RuneInfo
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// Riot API response models
public class RiotMatchResponse
{
    public MatchMetadata Metadata { get; set; } = new();
    public MatchInfo Info { get; set; } = new();
}

public class MatchMetadata
{
    public string MatchId { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = new();
}

public class MatchInfo
{
    public long GameDuration { get; set; }
    public string GameVersion { get; set; } = string.Empty;
    public List<MatchParticipant> Participants { get; set; } = new();
}

public class MatchParticipant
{
    public string ChampionName { get; set; } = string.Empty;
    public int ChampionId { get; set; }
    public bool Win { get; set; }
    public string TeamPosition { get; set; } = string.Empty;
    public string Lane { get; set; } = string.Empty;
    public int Item0 { get; set; }
    public int Item1 { get; set; }
    public int Item2 { get; set; }
    public int Item3 { get; set; }
    public int Item4 { get; set; }
    public int Item5 { get; set; }
    public int Item6 { get; set; }
    public int Summoner1Id { get; set; }
    public int Summoner2Id { get; set; }
    public MatchPerks Perks { get; set; } = new();
}

public class MatchPerks
{
    public List<MatchPerkStyle> Styles { get; set; } = new();
}

public class MatchPerkStyle
{
    public int Style { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<MatchPerkSelection> Selections { get; set; } = new();
}

public class MatchPerkSelection
{
    public int Perk { get; set; }
}

public class SummonerAccount
{
    public string Puuid { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string TagLine { get; set; } = string.Empty;
}

public class SummonerResponse
{
    public string Puuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class LeagueList
{
    public List<LeagueEntry> Entries { get; set; } = new();
}

public class LeagueEntry
{
    public string SummonerId { get; set; } = string.Empty;
    public string SummonerName { get; set; } = string.Empty;
    public int LeaguePoints { get; set; }
}