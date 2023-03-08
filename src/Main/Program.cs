using System.Net;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;

var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;
Console.CancelKeyPress += (s, e) =>
{
    Console.WriteLine("\n\nCTRL+C pressed, canceling...");
    cts.Cancel();
    e.Cancel = true;
};

Console.WriteLine("Starting LiazDakarGalleryDownloader instance.");

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false)
    .Build();

AppOptions appOptions = new ();
configuration.Bind(nameof(AppOptions), appOptions);

try
{
    foreach (var albumPair in appOptions.ThrowWhenNotValid().Albums)
    {
        var outputDirectory = Path.Combine(appOptions.OutputDirectory, $"LiazCamion-Liaz{albumPair.Key}");
        await CreateAlbumDirectoryAsync(albumPair, outputDirectory);
        var imagesToDownload = await GetAlbumPhotoLinks(albumPair.Value);
        if (appOptions.UseParallelDownloading)
        {
            await DownloadAlbumInParallelAsync(outputDirectory, imagesToDownload);
        }
        else
        {
            await DownloadAlbumSeriallyAsync(outputDirectory, imagesToDownload);
        }

    }
}
catch (TaskCanceledException e)
{
    // Cancellation is correct.
}
catch (Exception error)
{
    Console.WriteLine("\n\nLiazDakarGalleryDownloader terminated unexpectedly.");
    while (error is not null)
    {
        Console.WriteLine($" > ({error.GetType().Name}) {error.Message}");
        error = error.InnerException;
    }

    return 1;
}
finally
{
    Console.WriteLine("\n\nLiazDakarGalleryDownloader finished.");
}

return 0;


// http://liaz-dakar.com/components/com_eventgallery/helpers/image.php?
//   option=com_eventgallery
//   mode=uncrop
//   width=50
//   view=resizeimage
//   folder=LIAZ Dakar 90
//   file=17.jpg
// http://liaz-dakar.com/components/com_eventgallery/helpers/image.php?option=com_eventgallery&mode=full&view=resizeimage&folder=LIAZ Dakar 90&file=17.jpg
async Task<Uri[]> GetAlbumPhotoLinks(Uri albumUri)
{
    try
    {
        // Read the page content
        using HttpClient client = new ();
        var html = await client.GetStringAsync(albumUri, cancellationToken);

        //Load the document with the last book node.
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        var urls =
            htmlDoc.DocumentNode.Descendants("img")
                .Where(node => string.IsNullOrWhiteSpace(node.GetAttributeValue("longdesc", "")) is false)
                .Select(node => new Uri(HttpUtility.UrlDecode(node.GetAttributeValue("longdesc", ""))))
                .ToArray();
        return urls;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR ALBUM ({ex.Message}): {albumUri}");
        return Array.Empty<Uri>();
    }
}

async Task DownloadAlbumInParallelAsync(string outputDirectory, Uri[] imagesToDownload)
{
    var opt = new ParallelOptions
    {
        MaxDegreeOfParallelism = appOptions.MaxDegreeOfParallelism,
        CancellationToken = cancellationToken
    };

    await Parallel.ForEachAsync(imagesToDownload, opt, async (image, _) => { await DownloadImageAsync(outputDirectory, image); });
}

async Task DownloadAlbumSeriallyAsync(string outputDirectory, Uri[] imagesToDownload)
{
    foreach (var image in imagesToDownload)
    {
        await DownloadImageAsync(outputDirectory, image);
    }
}

async Task CreateAlbumDirectoryAsync(KeyValuePair<string, Uri> albumPair, string outputDirectory)
{
    if (Directory.Exists(outputDirectory) is false)
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await using var writer = File.CreateText(Path.Combine(outputDirectory, "_source.txt"));
    await writer.WriteAsync(albumPair.Value.AbsoluteUri);
    writer.Close();
}

async Task DownloadImageAsync(string outputDirectory, Uri imageUri)
{
    // Extract file name
    var query = HttpUtility.ParseQueryString(WebUtility.HtmlDecode(imageUri.Query));
    var file = query.Get("file") ?? "fake";
    var newUri = new Uri($"http://liaz-dakar.com/components/com_eventgallery/helpers/image.php?option={query.Get("option")}&mode=full&view=resizeimage&folder={query.Get("folder")}&file={file}");

    var fileName = file.ToLower();
    if (string.IsNullOrEmpty(fileName))
    {
        Console.WriteLine($"Cannot parse file parameter from '{imageUri.Query}' query");
        return;
    }

    Console.WriteLine($"\n--[ image: {fileName} ]-------------------\nDownloading url: {newUri}");

    // Read the page content
    using HttpClient client = new ();
    try
    {
        var outputFileName = Path.Combine(outputDirectory, fileName).ToLower();
        if (File.Exists(outputFileName))
        {
            Console.WriteLine($"File skip, already downloaded: '{outputFileName}'");
            return;
        }

        var fileBytes = await client.GetByteArrayAsync(newUri, cancellationToken);
        if (fileBytes.Length < 1_000)
        {
            return;
        }

        await File.WriteAllBytesAsync(outputFileName, fileBytes, cancellationToken);
        Console.WriteLine($"FileName: {outputFileName}");

        // File.SetCreationTime(outputFilename, photo.Date);
        // File.SetLastWriteTime(outputFilename, photo.Date);
        // File.SetLastAccessTime(outputFilename, photo.Date);
    }
    catch (HttpRequestException e)
    {
        Console.WriteLine($"\nERROR IMAGE ({e.GetType().Name}) {e.Message}\n URL: {imageUri}");
    }

    Console.WriteLine($"\nFile '{file}' are stored in directory: '{outputDirectory}'");
}
