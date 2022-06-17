using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Maroon.IntegrationTesting.DockerClient.Tests;

[SetUpFixture]
public class ContainerSetup
{
    public static HttpClient AppClient;
    
    private IDockerContext? _context;
    private DockerContainer? _sqlContainer;
    private DockerContainer? _appContainer;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _context = DockerContext.Create(TestContext.WriteLine);
        var network = await _context.Networks.EnsureNetworkExists("sample-application");

        await StartSql(network);

        var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".."));
        var image = await _context.Images.Build("sample-application", "latest", path, null, "Maroon.IntegrationTesting.DockerClient.SampleApplication/Dockerfile");
        _appContainer = await _context.Containers.Start(network, "app", image,
            new Dictionary<string, string>
            {
            },
            new[] {8080},
            true
        );

        AppClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:8080")
        };

        for (var i = 0; i < 20; i++)
        {
            try
            {
                var response = await AppClient.GetAsync("/healthz");
                if (response.IsSuccessStatusCode)
                    break;
            }
            catch
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(20));
            }
        }
    }

    private async Task StartSql(DockerNetwork network)
    {
        var image = await _context!.Images.Pull("mcr.microsoft.com/azure-sql-edge", "latest");
        _sqlContainer = await _context.Containers.Start(network, "sql", image,
            new Dictionary<string, string>
            {
                ["ACCEPT_EULA"] = "1",
                ["MSSQL_SA_PASSWORD"] = "yourStrong(!)Password",
                ["MSSQL_PID"] = "Premium",
            },
            new[] {1433},
            false
        );
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await _appContainer!.DisposeAsync();
        await _sqlContainer!.DisposeAsync();
        _context?.Dispose();
    }
}
