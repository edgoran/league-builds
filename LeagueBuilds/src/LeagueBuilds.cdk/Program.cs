using Amazon.CDK;

namespace LeagueBuilds.Cdk;

class Program
{
    static void Main(string[] args)
    {
        var app = new App();

        new LeagueBuildsStack(app, "LeagueBuildsStack", new StackProps
        {
            Env = new Amazon.CDK.Environment
            {
                Region = "eu-west-2"
            }
        });

        app.Synth();
    }
}