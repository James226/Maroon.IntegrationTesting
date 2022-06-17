using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Maroon.IntegrationTesting.DockerClient;

public readonly record struct DockerImage(string Image, string Tag);

public class ImageOperations
{
    private readonly IDockerContext _context;

    public ImageOperations(IDockerContext context)
    {
        _context = context;
    }
    
    public async Task<DockerImage> Pull(string image, string tag)
    {
        _context.Output($"[{DateTime.UtcNow:u}] [{image}]: Pulling image");

        var progress = new Progress<JSONMessage>(m =>
        {
            if (m.Status != "Downloading" && m.Status != "Extracting")
                _context.Output($"[{DateTime.UtcNow:u}] [{image}]: {m.Status}");
        });
        await _context.DockerClient.Images.CreateImageAsync(new ImagesCreateParameters
        {
            FromImage = image,
            Tag = tag
        }, new AuthConfig(), progress);
           
        _context.Output($"[{DateTime.UtcNow:u}] [{image}]: Pulled image");
        return new DockerImage(image, tag);
    }

    public async Task<DockerImage> Build(string image,
        string tag,
        string path,
        Dictionary<string, string>? buildArgs = null,
        string dockerfile = "Dockerfile")
    {
        _context.Output($"[{DateTime.UtcNow:u}] [{image}]: Building image");
        await using var ms = new MemoryStream();
        CompressDirectory(path, ms);

        ms.Seek(0, SeekOrigin.Begin);
        
        var log = new StringBuilder();
        var error = string.Empty;
        
        await _context.DockerClient.Images.BuildImageFromDockerfileAsync(new ImageBuildParameters
            {
                Tags = new List<string>
                {
                    $"{image}:{tag}"
                },
                BuildArgs = buildArgs,
                Dockerfile = dockerfile
            },
            ms,
            Array.Empty<AuthConfig>(),
            new Dictionary<string, string>(),
            new Progress<JSONMessage>(m =>
            {
                if (m.ErrorMessage != null) error = m.ErrorMessage;
                lock (log)
                {
                    log.Append(m.Stream);
                    for (var i = 0; i < log.Length; i++)
                    {
                        if (log[i] != '\n') continue;

                        var logMessage = log.ToString(0, i);
                        log.Remove(0, i + 1);
                        _context.Output.Invoke($"[{DateTime.UtcNow:u}] [{image}]: {logMessage}");
                        i = 0;
                    }
                }
            }),
            CancellationToken.None);

        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException($"Failed to build docker image: {error}");
        
        _context.Output($"[{DateTime.UtcNow:u}] [{image}]: Image built successfuly");
        return new DockerImage(image, tag);
    }

    public static DockerImage Local(string image, string tag)
    {
        return new DockerImage(image, tag);
    }

    private static void CompressDirectory(string path, Stream stream)
    {
        var dockerIgnore = Path.Combine(path, ".dockerignore");
        var excludes = File.Exists(dockerIgnore)
            ? File.ReadAllLines(dockerIgnore)
            : Array.Empty<string>();
        
        Matcher matcher = new();
        matcher.AddInclude("**/*");
        matcher.AddExcludePatterns(excludes);

        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(path)));

        using var archive = ArchiveFactory.Create(ArchiveType.Tar);

        foreach (var enumerateFile in result.Files.Select(f => Path.Combine(path, f.Path)))
        {
            var fileInfo = new FileInfo(enumerateFile);
            archive.AddEntry(enumerateFile.Substring(path.Length),
                fileInfo.OpenRead(),
                true,
                fileInfo.Length,
                fileInfo.LastWriteTime);
        }
        
        archive.SaveTo(stream, new WriterOptions(CompressionType.None));
    }
}