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

    // Get detailed champion data (includes abilities, skins)
    public async Task<ChampionDetailResponse?> GetChampionDetailAsync(string championId, string patch)
    {
        var url = $"https://ddragon.leagueoflegends.com/cdn/{patch}/data/en_US/champion/{championId}.json";

        Console.WriteLine($"Fetching champion detail from: {url}");

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Champion detail fetch failed: {response.StatusCode}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ChampionDetailResponse>(json, JsonOptions);
    }

    // Get summoner by Riot ID
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

    // Get match IDs with pagination
    public async Task<List<string>> GetMatchIdsPagedAsync(string puuid, int totalCount = 100)
    {
        var allMatchIds = new List<string>();
        var batchSize = 100; // Riot max per call
        var start = 0;

        while (allMatchIds.Count < totalCount)
        {
            var remaining = totalCount - allMatchIds.Count;
            var count = Math.Min(batchSize, remaining);

            var url = $"https://{_region}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?start={start}&count={count}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Riot-Token", _apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) break;

            var batch = await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions) ?? new();
            if (batch.Count == 0) break;

            allMatchIds.AddRange(batch);
            start += batch.Count;

            await Task.Delay(50);
        }

        return allMatchIds;
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

    // Get Challenger players (includes PUUIDs)
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

    // Get Grandmaster players
    public async Task<List<LeagueEntry>> GetGrandmasterPlayersAsync()
    {
        var url = $"https://{_platform}.api.riotgames.com/lol/league/v4/grandmasterleagues/by-queue/RANKED_SOLO_5x5";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Grandmaster fetch failed: {response.StatusCode}");
            return new();
        }

        var league = await response.Content.ReadFromJsonAsync<LeagueList>(JsonOptions);
        return league?.Entries ?? new();
    }

    // Get players by rank
    public async Task<List<LeagueEntry>> GetPlayersByRankAsync(string tier, string division, int page = 1)
    {
        var url = $"https://{_platform}.api.riotgames.com/lol/league/v4/entries/RANKED_SOLO_5x5/{tier}/{division}?page={page}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Rank fetch failed for {tier} {division}: {response.StatusCode}");
            return new();
        }

        return await response.Content.ReadFromJsonAsync<List<LeagueEntry>>(JsonOptions) ?? new();
    }

    // Get summoner info by PUUID (for profile icon, level)
    public async Task<SummonerInfo?> GetSummonerByPuuidAsync(string puuid)
    {
        var url = $"https://{_platform}.api.riotgames.com/lol/summoner/v4/summoners/by-puuid/{puuid}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<SummonerInfo>(JsonOptions);
    }

    // Get champion mastery for a player
    public async Task<List<ChampionMastery>> GetChampionMasteryAsync(string puuid)
    {
        var url = $"https://{_platform}.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-puuid/{puuid}/top?count=10";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new();

        return await response.Content.ReadFromJsonAsync<List<ChampionMastery>>(JsonOptions) ?? new();
    }

    // Get single champion mastery
    public async Task<ChampionMastery?> GetChampionMasteryByIdAsync(string puuid, int championId)
    {
        var url = $"https://{_platform}.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-puuid/{puuid}/by-champion/{championId}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<ChampionMastery>(JsonOptions);
    }

    // Get ranked stats for a player
    public async Task<List<RankedEntry>> GetRankedStatsAsync(string puuid)
    {
        // First get summoner ID from PUUID
        var summoner = await GetSummonerByPuuidAsync(puuid);
        if (summoner == null) return new();

        var url = $"https://{_platform}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Token", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Ranked stats fetch failed: {response.StatusCode}");
            return new();
        }

        return await response.Content.ReadFromJsonAsync<List<RankedEntry>>(JsonOptions) ?? new();
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
    public List<string> Tags { get; set; } = new();
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

// Detailed champion data from Data Dragon
public class ChampionDetailResponse
{
    public Dictionary<string, ChampionDetail> Data { get; set; } = new();
}

public class ChampionDetail
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Lore { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public ChampionDetailInfo Info { get; set; } = new();
    public ChampionPassive Passive { get; set; } = new();
    public List<ChampionSpell> Spells { get; set; } = new();
    public List<ChampionSkin> Skins { get; set; } = new();
    public List<ChampionRecommended> Recommended { get; set; } = new();
}

public class ChampionDetailInfo
{
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Magic { get; set; }
    public int Difficulty { get; set; }
}

public class ChampionPassive
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ImageInfo Image { get; set; } = new();
}

public class ChampionSpell
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ImageInfo Image { get; set; } = new();
}

public class ChampionSkin
{
    public int Num { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Chromas { get; set; }
}
public class ChampionRecommended
{
    public string Map { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<ChampionRecommendedBlock> Blocks { get; set; } = new();
}

public class ChampionRecommendedBlock
{
    public string Type { get; set; } = string.Empty;
    public List<ChampionRecommendedItem> Items { get; set; } = new();
}

public class ChampionRecommendedItem
{
    public string Id { get; set; } = string.Empty;
    public int Count { get; set; }
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
    public long GameCreation { get; set; }
    public string GameVersion { get; set; } = string.Empty;
    public string GameMode { get; set; } = string.Empty;
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
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int TotalMinionsKilled { get; set; }
    public int NeutralMinionsKilled { get; set; }
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

public class LeagueList
{
    public List<LeagueEntry> Entries { get; set; } = new();
}

public class LeagueEntry
{
    public string SummonerId { get; set; } = string.Empty;
    public string SummonerName { get; set; } = string.Empty;
    public string Puuid { get; set; } = string.Empty;
    public int LeaguePoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
}

public class SummonerInfo
{
    public string Puuid { get; set; } = string.Empty;
    public int ProfileIconId { get; set; }
    public long SummonerLevel { get; set; }
}

public class ChampionMastery
{
    public string Puuid { get; set; } = string.Empty;
    public int ChampionId { get; set; }
    public int ChampionLevel { get; set; }
    public int ChampionPoints { get; set; }
}

public class RankedEntry
{
    public string QueueType { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public int LeaguePoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
}