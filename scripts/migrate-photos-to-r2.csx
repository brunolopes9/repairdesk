#r "nuget: AWSSDK.S3, 4.0.23.3"

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

var root = Environment.GetEnvironmentVariable("LOCAL_PHOTOS_ROOT") ?? "/data/photos";
var accountId = Required("Storage__R2__AccountId");
var accessKey = Required("Storage__R2__AccessKey");
var secret = Required("Storage__R2__Secret");
var bucket = Required("Storage__R2__Bucket");
var dryRun = !string.Equals(Environment.GetEnvironmentVariable("DRY_RUN"), "false", StringComparison.OrdinalIgnoreCase);

var config = new AmazonS3Config
{
    ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
    AuthenticationRegion = "auto",
    ForcePathStyle = true,
};

using var s3 = new AmazonS3Client(new BasicAWSCredentials(accessKey, secret), config);

var files = Directory.Exists(root)
    ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList()
    : throw new DirectoryNotFoundException(root);

Console.WriteLine($"Found {files.Count} files under {root}.");
Console.WriteLine(dryRun
    ? "DRY_RUN=true: no upload will be performed. Set DRY_RUN=false to upload."
    : $"Uploading to R2 bucket {bucket}.");

foreach (var file in files)
{
    var key = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
    Console.WriteLine($"{(dryRun ? "Would upload" : "Uploading")} {key}");

    if (dryRun)
        continue;

    await using var stream = File.OpenRead(file);
    await s3.PutObjectAsync(new PutObjectRequest
    {
        BucketName = bucket,
        Key = key,
        InputStream = stream,
        ContentType = GuessContentType(file),
        AutoCloseStream = false,
    });
}

Console.WriteLine("Done. StorageKey values do not need to change if keys already match tenants/{tenantId}/...");

static string Required(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} is required.");

static string GuessContentType(string file) =>
    Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
