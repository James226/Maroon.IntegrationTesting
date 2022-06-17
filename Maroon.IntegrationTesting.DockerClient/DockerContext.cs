using System.Runtime.InteropServices;
using Docker.DotNet;

namespace Maroon.IntegrationTesting.DockerClient;

public interface IDockerContext : IDisposable
{
    IDockerClient DockerClient { get; }
    Action<string> Output { get; }
    ImageOperations Images { get; }
    ContainerOperations Containers { get; set; }
    NetworkOperations Networks { get; set; }
}

public class DockerContext : IDockerContext
{
    private const string WindowsDockerPath = "npipe://./pipe/docker_engine";
    private const string UnixDockerPath = "unix:///var/run/docker.sock";

    public IDockerClient DockerClient { get; }
    public ImageOperations Images { get; set; }
    public ContainerOperations Containers { get; set; }
    public NetworkOperations Networks { get; set; }



    public Action<string> Output { get; }

    private DockerContext(Action<string> output, IDockerClient client)
    {
        DockerClient = client;
        Output = output;
        Images = new ImageOperations(this);
        Containers = new ContainerOperations(this);
        Networks = new NetworkOperations(this);
    }

    public static IDockerContext Create(Action<string> output)
    {
        var dockerSocketAddress = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsDockerPath
            : UnixDockerPath;

        var dockerClient = new DockerClientConfiguration(new Uri(dockerSocketAddress)).CreateClient();
        return new DockerContext(output, dockerClient);
    }


    public void Dispose()
    {
        DockerClient.Dispose();
    }
}