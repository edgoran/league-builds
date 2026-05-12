namespace LeagueBuilds.Api.Models;

public class ChampionDetailStats
{
    public string ChampionName { get; set; } = string.Empty;
    public string ChampionId { get; set; } = string.Empty;
    public string ChampionIconUrl { get; set; } = string.Empty;
    public int MasteryLevel { get; set; }
    public int MasteryPoints { get; set; }
    public string MasteryPointsFormatted { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int GamesAnalysed { get; set; }  // Total matches checked
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinRate { get; set; }
    public double AvgKills { get; set; }
    public double AvgDeaths { get; set; }
    public double AvgAssists { get; set; }
    public double AvgKda { get; set; }
    public string KdaString { get; set; } = string.Empty;
    public double AvgCreepScore { get; set; }
    public List<PopularItem> MostBuiltItems { get; set; } = new();
    public List<MatchSummary> MatchHistory { get; set; } = new();
    public string Patch { get; set; } = string.Empty;
}

public class PopularItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int TimesBuilt { get; set; }
}