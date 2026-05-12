namespace LeagueBuilds.Api.Models;

public class PlayerProfile
{
    public string Puuid { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string TagLine { get; set; } = string.Empty;
    public int ProfileIconId { get; set; }
    public long SummonerLevel { get; set; }
    public string ProfileIconUrl { get; set; } = string.Empty;
    public List<MatchSummary> RecentMatches { get; set; } = new();
    public List<ChampionStats> TopChampions { get; set; } = new();
    public List<MasteryChampionInfo> TopMasteryChampions { get; set; } = new();
    public string Patch { get; set; } = string.Empty;
    public List<RankedInfo> RankedStats { get; set; } = new();
}

public class MatchSummary
{
    public string MatchId { get; set; } = string.Empty;
    public string ChampionName { get; set; } = string.Empty;
    public string ChampionId { get; set; } = string.Empty;
    public string ChampionIconUrl { get; set; } = string.Empty;
    public bool Win { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public double Kda { get; set; }
    public string KdaString { get; set; } = string.Empty;
    public int CreepScore { get; set; }
    public string Role { get; set; } = string.Empty;
    public long GameDuration { get; set; }
    public string GameDurationString { get; set; } = string.Empty;
    public string GameMode { get; set; } = string.Empty;
    public long GameCreation { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public List<int> ItemIds { get; set; } = new();
    public List<string> ItemIconUrls { get; set; } = new();
    public int Spell1Id { get; set; }
    public int Spell2Id { get; set; }
}

public class ChampionStats
{
    public string ChampionName { get; set; } = string.Empty;
    public string ChampionId { get; set; } = string.Empty;
    public string ChampionIconUrl { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinRate { get; set; }
    public double AvgKills { get; set; }
    public double AvgDeaths { get; set; }
    public double AvgAssists { get; set; }
    public double AvgKda { get; set; }
    public string KdaString { get; set; } = string.Empty;
}

public class MasteryChampionInfo
{
    public string ChampionName { get; set; } = string.Empty;
    public string ChampionId { get; set; } = string.Empty;
    public string ChampionIconUrl { get; set; } = string.Empty;
    public int MasteryLevel { get; set; }
    public int MasteryPoints { get; set; }
    public string MasteryPointsFormatted { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public double WinRate { get; set; }
    public string KdaString { get; set; } = string.Empty;
}

public class RankedInfo
{
    public string QueueType { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public int LeaguePoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinRate { get; set; }
    public string TierIconUrl { get; set; } = string.Empty;
}