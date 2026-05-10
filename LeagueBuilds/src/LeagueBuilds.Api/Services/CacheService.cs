using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LeagueBuilds.Api.Models;

namespace LeagueBuilds.Api.Services;

public class CacheService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly TimeSpan _cacheExpiry;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CacheService(IAmazonDynamoDB dynamoDb, string tableName = "LeagueBuildsCache", TimeSpan? cacheExpiry = null)
    {
        _dynamoDb = dynamoDb;
        _tableName = tableName;
        _cacheExpiry = cacheExpiry ?? TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Try to get a cached champion build. Returns null if not found or expired.
    /// </summary>
    public async Task<ChampionBuild?> GetCachedBuildAsync(string championName, string role)
    {
        var key = BuildCacheKey(championName, role);

        try
        {
            var response = await _dynamoDb.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = key },
                    ["sk"] = new AttributeValue { S = "build" }
                }
            });

            if (response.Item == null || response.Item.Count == 0)
                return null;

            // Check if expired
            if (response.Item.TryGetValue("ttl", out var ttlValue))
            {
                var ttl = long.Parse(ttlValue.N);
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(ttl);

                if (DateTimeOffset.UtcNow > expiryTime)
                    return null; // Expired
            }

            // Deserialize the cached data
            if (response.Item.TryGetValue("data", out var dataValue))
            {
                return JsonSerializer.Deserialize<ChampionBuild>(dataValue.S, JsonOptions);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache read error: {ex.Message}");
            return null; // Treat cache errors as misses
        }
    }

    /// <summary>
    /// Store a champion build in the cache.
    /// </summary>
    public async Task SetCachedBuildAsync(string championName, string role, ChampionBuild build)
    {
        var key = BuildCacheKey(championName, role);
        var ttl = DateTimeOffset.UtcNow.Add(_cacheExpiry).ToUnixTimeSeconds();
        var data = JsonSerializer.Serialize(build, JsonOptions);

        try
        {
            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = key },
                    ["sk"] = new AttributeValue { S = "build" },
                    ["data"] = new AttributeValue { S = data },
                    ["championName"] = new AttributeValue { S = championName },
                    ["role"] = new AttributeValue { S = role },
                    ["patch"] = new AttributeValue { S = build.Patch },
                    ["ttl"] = new AttributeValue { N = ttl.ToString() },
                    ["updatedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache write error: {ex.Message}");
            // Don't throw — caching failure shouldn't break the request
        }
    }

    /// <summary>
    /// Invalidate cache for a champion (e.g., when a new patch drops).
    /// </summary>
    public async Task InvalidateCacheAsync(string championName, string role)
    {
        var key = BuildCacheKey(championName, role);

        try
        {
            await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = key },
                    ["sk"] = new AttributeValue { S = "build" }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache invalidation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Invalidate all cached data (e.g., on major patch update).
    /// </summary>
    public async Task InvalidateAllAsync()
    {
        try
        {
            // Scan for all items and delete them
            var scanResponse = await _dynamoDb.ScanAsync(new ScanRequest
            {
                TableName = _tableName,
                ProjectionExpression = "pk, sk"
            });

            foreach (var item in scanResponse.Items)
            {
                await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = _tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = item["pk"],
                        ["sk"] = item["sk"]
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache invalidate all error: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if the cache has a valid (non-expired) entry for a champion.
    /// </summary>
    public async Task<bool> HasValidCacheAsync(string championName, string role)
    {
        var build = await GetCachedBuildAsync(championName, role);
        return build != null;
    }

    private string BuildCacheKey(string championName, string role)
    {
        return $"CHAMPION#{championName.ToLower()}#ROLE#{role.ToLower()}";
    }
}