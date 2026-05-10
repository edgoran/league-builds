using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Constructs;
using Amazon.CDK.AWS.SSM;

using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;
using Function = Amazon.CDK.AWS.Lambda.Function;
using FunctionProps = Amazon.CDK.AWS.Lambda.FunctionProps;
using Amazon.CDK.AWS.IAM;

namespace LeagueBuilds.Cdk;

public class LeagueBuildsStack : Stack
{
    public LeagueBuildsStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // ============================================================
        // DynamoDB Table — Cache for champion builds
        // ============================================================
        var cacheTable = new Table(this, "LeagueBuildsCache", new TableProps
        {
            TableName = "LeagueBuildsCache",
            PartitionKey = new Attribute
            {
                Name = "pk",
                Type = AttributeType.STRING
            },
            SortKey = new Attribute
            {
                Name = "sk",
                Type = AttributeType.STRING
            },
            BillingMode = BillingMode.PAY_PER_REQUEST, // On-demand — no minimum cost
            RemovalPolicy = RemovalPolicy.DESTROY, // Delete table when stack is destroyed
            TimeToLiveAttribute = "ttl" // Auto-delete expired items
        });

        // ============================================================
        // Lambda Function — Get Champion Builds
        // ============================================================
        var getChampionBuildsFunction = new Function(this, "GetChampionBuilds", new FunctionProps
        {
            FunctionName = "LeagueBuilds-GetChampionBuilds",
            Runtime = Runtime.DOTNET_8,
            Handler = "LeagueBuilds.Api::LeagueBuilds.Api.Functions.GetChampionBuilds::HandleAsync",
            Code = Code.FromAsset("../LeagueBuilds.Api/bin/Release/net8.0/publish"),
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                ["RIOT_API_KEY_PARAM"] = "/league-builds/riot-api-key",
                ["CACHE_TABLE_NAME"] = cacheTable.TableName
            }
        });
        
        // ============================================================
        // Lambda Function — Get Champions List
        // ============================================================
        var getChampionsFunction = new Function(this, "GetChampions", new FunctionProps
        {
            FunctionName = "LeagueBuilds-GetChampions",
            Runtime = Runtime.DOTNET_8,
            Handler = "LeagueBuilds.Api::LeagueBuilds.Api.Functions.GetChampions::HandleAsync",
            Code = Code.FromAsset("../LeagueBuilds.Api/bin/Release/net8.0/publish"),
            MemorySize = 256,
            Timeout = Duration.Seconds(10),
            Environment = new Dictionary<string, string>
            {
                ["RIOT_API_KEY_PARAM"] = "/league-builds/riot-api-key"
            }
        });

        // Grant both Lambda functions access to read the SSM parameter
        var ssmPolicy = new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "ssm:GetParameter",
                "ssm:GetParameters",
                "kms:Decrypt"
            },
            Resources = new[]
            {
                $"arn:aws:ssm:{Region}:{Account}:parameter/league-builds/riot-api-key",
                $"arn:aws:kms:{Region}:{Account}:key/*"
            }
        });

        getChampionBuildsFunction.AddToRolePolicy(ssmPolicy);
        getChampionsFunction.AddToRolePolicy(ssmPolicy);

        // Grant Lambda functions access to DynamoDB
        cacheTable.GrantReadWriteData(getChampionBuildsFunction);

        // ============================================================
        // API Gateway — REST API
        // ============================================================
        var api = new RestApi(this, "LeagueBuildsApi", new RestApiProps
        {
            RestApiName = "League Builds API",
            Description = "API for League of Legends champion build data",
            DefaultCorsPreflightOptions = new CorsOptions
            {
                AllowOrigins = Cors.ALL_ORIGINS,
                AllowMethods = Cors.ALL_METHODS,
                AllowHeaders = new[] { "Content-Type" }
            }
        });

        // GET /champions
        var championsResource = api.Root.AddResource("champions");
        championsResource.AddMethod("GET", new LambdaIntegration(getChampionsFunction));

        // GET /champion/{name}
        var championResource = api.Root.AddResource("champion");
        var championNameResource = championResource.AddResource("{name}");
        championNameResource.AddMethod("GET", new LambdaIntegration(getChampionBuildsFunction));

        // ============================================================
        // S3 Bucket — Frontend hosting
        // ============================================================
        var frontendBucket = new Bucket(this, "FrontendBucket", new BucketProps
        {
            BucketName = $"league-builds-frontend-{Account}",
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL
        });

        // ============================================================
        // CloudFront Distribution — CDN
        // ============================================================
        var distribution = new Distribution(this, "FrontendDistribution", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(frontendBucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS
            },
            DefaultRootObject = "index.html",
            ErrorResponses = new[]
            {
                new ErrorResponse
                {
                    HttpStatus = 404,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html" // SPA support
                }
            }
        });

        // ============================================================
        // Outputs — URLs you'll need
        // ============================================================
        new CfnOutput(this, "ApiUrl", new CfnOutputProps
        {
            Value = api.Url,
            Description = "API Gateway endpoint URL"
        });

        new CfnOutput(this, "FrontendUrl", new CfnOutputProps
        {
            Value = $"https://{distribution.DistributionDomainName}",
            Description = "CloudFront frontend URL"
        });

        new CfnOutput(this, "FrontendBucketName", new CfnOutputProps
        {
            Value = frontendBucket.BucketName,
            Description = "S3 bucket for frontend deployment"
        });
    }
}