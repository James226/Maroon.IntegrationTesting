using System.Net;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Maroon.IntegrationTesting.DockerClient;

public class DockerContainer : IAsyncDisposable
{
    public IPAddress IpAddress => IPAddress.Parse(_container.NetworkSettings.Networks.First().Value.IPAddress);

    private readonly IDockerContext _context;
    private readonly string _name;
    private readonly ContainerInspectResponse _container;

    public DockerContainer(IDockerContext context, string name, ContainerInspectResponse container)
    {
        _context = context;
        _name = name;
        _container = container;
    }

    public async ValueTask DisposeAsync()
    {
        _context.Output($"Stopping container: {_name}");
        var containers = await _context.DockerClient.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool>
                {
                    [_name] = true
                }
            }
        });
        var container = containers.FirstOrDefault(c => c.Names.Contains($"/{_name}"));
        
        if (container != null)
        {
            await _context.DockerClient.Containers.StopContainerAsync(_name, new ContainerStopParameters(), CancellationToken.None);
        }
        _context.Output($"Stopped container: {_name}");
    }

    public async Task WaitForExit()
    {
        await _context.DockerClient.Containers.WaitContainerAsync(_name);
    }
}

public class ContainerOperations
{
    private readonly IDockerContext _context;

    public ContainerOperations(IDockerContext context)
    {
        _context = context;
    }
    
    public async Task<DockerContainer> Start(DockerNetwork network,
        string name,
        DockerImage image,
        IDictionary<string, string> environmentVariables,
        ICollection<int> ports,
        bool logOutput,
        params string[] cmd)
    {
        await StopContainer(name);

        var command = cmd.Length > 0 ? cmd.ToArray() : null;
        await StartContainer(name, logOutput, new CreateContainerParameters
        {
            Image = $"{image.Image}:{image.Tag}",
            Name = name,
            Hostname = name,
            Env = environmentVariables.Select(e => $"{e.Key}={e.Value}").ToList(),
            Cmd = command,
            ExposedPorts = ports.ToDictionary(p => p.ToString(), _ => new EmptyStruct()),
            HostConfig = new HostConfig
            {
                AutoRemove = true,
                NetworkMode = network.Id,
                PortBindings = ports.ToDictionary(p => p.ToString(), p => (IList<PortBinding>)new List<PortBinding> { new() { HostPort = p.ToString()} } )
            }
        });

        var container = await _context.DockerClient.Containers.InspectContainerAsync(name);
            
        return new DockerContainer(_context, name, container);
    }

    private async Task StartContainer(string containerName, bool logOutput,
        CreateContainerParameters parameters)
    {
        _context.Output.Invoke($"[{DateTime.UtcNow:u}] [{containerName}]: Starting Container...");

        await _context.DockerClient.Containers.CreateContainerAsync(parameters);


        if (logOutput)
        {
            var result = await _context.DockerClient.Containers.AttachContainerAsync(containerName, false,
                new ContainerAttachParameters {Stream = true, Stdout = true, Stderr = true}, CancellationToken.None);
            ReadBuffer(result, containerName);
        }

        await _context.DockerClient.Containers.StartContainerAsync(containerName,
            new ContainerStartParameters());
        _context.Output.Invoke($"[{DateTime.UtcNow:u}] [{containerName}]: Container Started");
    }
    
    private void ReadBuffer(MultiplexedStream stream, string containerName, byte[]? buffer = null, StringBuilder? log = null)
    {
        buffer ??= new byte[1024];
        
        log ??= new StringBuilder();
        
        void ProcessResponse(Task<MultiplexedStream.ReadResult> t)
        {
            log.Append(Encoding.UTF8.GetString(buffer.Take(t.Result.Count).ToArray()));
            for (var i = 0; i < log.Length; i++)
            {
                if (log[i] != '\n') continue;

                var logMessage = log.ToString(0, i);
                log.Remove(0, i + 1);
                _context.Output.Invoke($"[{DateTime.UtcNow:u}] [{containerName}]: {logMessage}");
                i = 0;
            }

            if (!t.Result.EOF)
            {
                ReadBuffer(stream, containerName, buffer, log);
            }
            else
            {
                if (log.Length > 0)
                    _context.Output.Invoke($"[{DateTime.UtcNow:u}] [{containerName}]: {log}");
                _context.Output.Invoke($"[{DateTime.UtcNow:u}] [{containerName}]: Process complete");
            }
        }

        stream.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None)
            .ContinueWith(ProcessResponse);
    }

    private async Task StopContainer(string name)
    {
        var container = await FindContainer(name);

        if (container != null)
        {
            await _context.DockerClient.Containers.StopContainerAsync(name, new ContainerStopParameters(), CancellationToken.None);
            for (var i = 0; i < 20 && await FindContainer(name) != null; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20));
            }
        }
    }

    private async Task<ContainerListResponse?> FindContainer(string name)
    {
        var containers = await _context.DockerClient.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool>
                {
                    [name] = true
                }
            }
        });
        var container = containers.FirstOrDefault(c => c.Names.Contains($"/{name}"));
        return container;
    }
}