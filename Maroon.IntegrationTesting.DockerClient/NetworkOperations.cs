using Docker.DotNet.Models;

namespace Maroon.IntegrationTesting.DockerClient;

public readonly record struct DockerNetwork(string Id, string Gateway);

public class NetworkOperations
{
    private readonly IDockerContext _context;

    public NetworkOperations(IDockerContext context)
    {
        _context = context;
    }
    
    public async Task<DockerNetwork> EnsureNetworkExists(string name)
    {
        var network = await GetNetwork(name);
        if (network.HasValue) return network.Value;

        var param = new NetworksCreateParameters
        {
            Attachable = true,
            CheckDuplicate = true,
            Name = name
        };
        await _context.DockerClient.Networks.CreateNetworkAsync(param);

        return await GetNetwork(name) ?? throw new Exception("Unable to create network");
    }

    private async Task<DockerNetwork?> GetNetwork(string name)
    {
        var networks = await _context.DockerClient.Networks.ListNetworksAsync();
        var network = networks.FirstOrDefault(n => n.Name == name);
        if (network == null) return null;
        
        return new DockerNetwork(network.ID, network.IPAM.Config.FirstOrDefault()!.Gateway);
    }
}