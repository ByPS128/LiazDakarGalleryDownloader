using System.ComponentModel.DataAnnotations;

public sealed class AppOptions
{
    public bool UseParallelDownloading { get; init; }

    [Required()]
    [Range(2, 100)]
    public int MaxDegreeOfParallelism { get; init; }

    [Required()]
    public string OutputDirectory { get; init; }

    public Dictionary<string, Uri> Albums { get; init; }
}
