using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Maroon.IntegrationTesting.DockerClient.Tests;

public class Tests
{
    private HttpResponseMessage? _response;

    [SetUp]
    public async Task Setup()
    {
        _response = await ContainerSetup.AppClient.GetAsync("/healthz");
    }

    [Test]
    public void ThenTheResponseIsSuccessful()
    {
        Assert.That(_response!.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}