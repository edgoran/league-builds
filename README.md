# League Stats

A full-stack web application for looking up League of Legends player statistics, match history, and champion information.

## Features

- Player search by Riot ID (name + tag)
- Recent match history with KDA, items, and game duration
- Per-champion statistics (win rate, average KDA, most built items)
- Champion mastery integration
- Ranked stats display
- Champion info pages (abilities, lore, skins)
- Responsive dark-themed UI

## Tech Stack

### Backend
- C# / .NET 8
- AWS Lambda (serverless compute)
- AWS API Gateway (REST API)
- AWS DynamoDB (caching)
- AWS SSM Parameter Store (secrets management)
- AWS CDK in C# (infrastructure as code)

### Frontend
- HTML / CSS / JavaScript
- Riot Data Dragon (champion images, item icons)

### External APIs
- Riot Games API (match history, player data, champion mastery)

## Architecture

```
Frontend (S3 + CloudFront)
    │
    ▼
API Gateway
    │
    ├── GET /champions → Lambda (GetChampions)
    ├── GET /champion/{name} → Lambda (GetChampionPage)
    └── GET /player/{name}/{tag} → Lambda (GetPlayerProfile)
                │
                ├── Riot Games API (match data, mastery)
                ├── Data Dragon (static champion/item data)
                └── DynamoDB (response caching)
```

## Deployment

### Prerequisites

- AWS CLI configured (`aws configure`)
- .NET 8 SDK
- AWS CDK CLI (`npm install -g aws-cdk`)
- Riot Games API key ([developer.riotgames.com](https://developer.riotgames.com))

### Setup

1. Store your Riot API key in SSM:

```bash
aws ssm put-parameter --name "/league-builds/riot-api-key" --value "RGAPI-your-key" --type SecureString --region eu-west-2
```

2. Publish the Lambda code:

```bash
cd src/LeagueBuilds.Api
dotnet publish -c Release -o bin/Release/net8.0/publish
```

3. Deploy:

```bash
cd src/LeagueBuilds.Cdk
cdk deploy
```

### Environment Variables (set by CDK)

| Variable | Purpose |
|----------|---------|
| `RIOT_API_KEY_PARAM` | SSM parameter name for Riot API key |
| `CACHE_TABLE_NAME` | DynamoDB table name |

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /champions` | List all champions with icons |
| `GET /champion/{name}` | Champion info (abilities, lore, skins) |
| `GET /champion/{name}?player={name}&tag={tag}` | Champion info with personal stats |
| `GET /player/{name}/{tag}` | Player profile, match history, mastery |

## Project Structure

```
league-builds/
├── src/
│   ├── LeagueBuilds.Api/
│   │   ├── Functions/
│   │   │   ├── GetChampions.cs
│   │   │   ├── GetChampionPage.cs
│   │   │   └── GetPlayerProfile.cs
│   │   ├── Services/
│   │   │   ├── RiotApiService.cs
│   │   │   ├── PlayerStatsService.cs
│   │   │   ├── CacheService.cs
│   │   │   └── ConfigService.cs
│   │   └── Models/
│   │       ├── PlayerProfile.cs
│   │       ├── ChampionPageData.cs
│   │       ├── ChampionDetailStats.cs
│   │       └── ApiResponse.cs
│   └── LeagueBuilds.Cdk/
│       ├── Program.cs
│       └── LeagueBuildsStack.cs
├── frontend/
│   ├── index.html
│   ├── styles.css
│   └── app.js
└── test/
    └── LeagueBuilds.Tests/
```

## Riot API Note

Development API keys expire every 24 hours. For persistent access, apply for a production key at [developer.riotgames.com](https://developer.riotgames.com).

To update an expired key:

```bash
aws ssm put-parameter --name "/league-builds/riot-api-key" --value "RGAPI-new-key" --type SecureString --region eu-west-2 --overwrite
```

## Cost

| Service | Monthly Cost |
|---------|-------------|
| Lambda | ~$0 (pay per request) |
| API Gateway | ~$0-1 |
| DynamoDB (on-demand) | ~$0 |
| S3 + CloudFront | ~$0.50 |
| **Total** | **Under $2/month** |

## Known Limitations

- Riot development keys expire every 24 hours
- Match history limited to most recent 200 games (API constraint)
- Champion mastery data reflects all-time play, stats are from recent matches only
- Some champion skins (certain named chromas) may still appear in the skins list
