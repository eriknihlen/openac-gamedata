using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAC.GameData.Ace;

/// <summary>
/// The ACE world-database release a build uses, as <c>ace-world.json</c> pins
/// it: where to download it, and the checksum it must have.
/// </summary>
internal sealed record AceWorldPin(
    [property: JsonPropertyName("repository")] string Repository,
    [property: JsonPropertyName("release")] string Release,
    [property: JsonPropertyName("asset")] string Asset,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("published")] DateTimeOffset Published)
{
    public const string FileName = "ace-world.json";

    public string DownloadUrl => $"https://github.com/{Repository}/releases/download/{Release}/{Asset}";

    public static AceWorldPin Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<AceWorldPin>(stream)
            ?? throw new InvalidDataException($"{path} is empty.");
    }

    /// <summary>Downloads the pinned asset into <paramref name="directory"/> unless a verified copy is there.</summary>
    public async Task<string> FetchAsync(string directory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, Asset);
        if (File.Exists(path) && HasChecksum(path))
            return path;
        string partial = path + ".part";
        using (var http = new HttpClient())
        using (Stream download = await http.GetStreamAsync(DownloadUrl, cancellationToken))
        using (FileStream file = File.Create(partial))
        {
            await download.CopyToAsync(file, cancellationToken);
        }
        if (!HasChecksum(partial))
        {
            File.Delete(partial);
            throw new InvalidDataException($"{DownloadUrl} does not have the pinned checksum {Sha256}.");
        }
        File.Move(partial, path, overwrite: true);
        return path;
    }

    public bool HasChecksum(string path)
    {
        using FileStream stream = File.OpenRead(path);
        string actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        return string.Equals(actual, Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
