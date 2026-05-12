using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

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
        _cacheExpiry = cacheExpiry ?? TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Try to get a cached item. Returns null if not found or expired.
    /// </summary>
    public async Task<T?> GetCachedAsync<T>(string key, string sortKey = "data") where T : class
    {
        try
        {
            var response = await _dynamoDb.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = key },
                    ["sk"] = new AttributeValue { S = sortKey }
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
                    return null;
            }

            if (response.Item.TryGetValue("data", out var dataValue))
            {
                return JsonSerializer.Deserialize<T>(dataValue.S, JsonOptions);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache read error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Store an item in the cache.
    /// </summary>
    public async Task SetCachedAsync<T>(string key, T data, string sortKey = "data") where T : class
    {
        var ttl = DateTimeOffset.UtcNow.Add(_cacheExpiry).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(data, JsonOptions);

        try
        {
            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = key },
                    ["sk"] = new AttributeValue { S = sortKey },
                    ["data"] = new AttributeValue { S = json },
                    ["ttl"] = new AttributeValue { N = ttl.ToString() },
                    ["updatedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache write error: {ex.Message}");
        }
    }

    /// <summary>
    /// Invalidate a cached item.
    /// </summary>
    public async Task InvalidateAsync(string key, string sortKey = "data")
    {
        try
        {
            await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = key },
                    ["sk"] = new AttributeValue { S = sortKey }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cache invalidation error: {ex.Message}");
        }
    }
}