namespace LeagueBuilds.Api.Models;

public class ChampionPageData
{
    public string ChampionId { get; set; } = string.Empty;
    public string ChampionName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Lore { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public ChampionAbilities Abilities { get; set; } = new();
    public List<SkinInfo> Skins { get; set; } = new();
    public ChampionDetailStats? PersonalStats { get; set; }
    public string Patch { get; set; } = string.Empty;
}

public class ChampionAbilities
{
    public AbilityInfo Passive { get; set; } = new();
    public AbilityInfo Q { get; set; } = new();
    public AbilityInfo W { get; set; } = new();
    public AbilityInfo E { get; set; } = new();
    public AbilityInfo R { get; set; } = new();
}

public class AbilityInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class SkinInfo
{
    public int SkinNum { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SplashUrl { get; set; } = string.Empty;
    public string LoadingUrl { get; set; } = string.Empty;
}