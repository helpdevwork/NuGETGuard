using NuGetGuard.Core;
using NuGetGuard.Core.Detectors;
using NuGetGuard.Core.Models;
using NuGetGuard.Core.Policy;
using NuGetGuard.Core.Reporting;
using NuGetGuard.Core.Resolvers;
using NuGetGuard.Core.VersionInfo;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
    {
        PrintHelp();
        return 0;
    }

    if (args[0] == "--version")
    {
        Console.WriteLine("NuGetGuard 0.1.0");
        return 0;
    }

    if (args[0] == "init")
        return HandleInit();

    if (args[0] == "scan")
        return await HandleScan(args.Skip(1).ToArray());

    Console.Error.WriteLine($"Unknown command: {args[0]}. Use --help for usage.");
    return 2;
}

static void PrintHelp()
{
    Console.WriteLine("""
        NuGetGuard — scan .NET dependencies for vulnerabilities, typosquats, and license changes.

        Usage:
          nugetguard scan [path]          Scan a project/solution directory (default: current directory)
            --format console|json|sarif   Output format (default: console)
            --output <file>               Write report to file instead of stdout
            --fail-on <severity>          Override policy severity threshold (None|Low|Moderate|High|Critical)
            --no-typosquat                Skip typosquat detection
            --no-license-check            Skip license change detection
            --no-version-check            Skip latest version check

          nugetguard init                 Create a default .nugetguard.json policy file
          nugetguard --version            Show version
          nugetguard --help               Show this help

        Exit codes:
          0  Clean or below threshold
          1  Findings at or above failOn threshold
          2  Tool error
        """);
}

static int HandleInit()
{
    var dir = Directory.GetCurrentDirectory();
    var path = Path.Combine(dir, ".nugetguard.json");

    if (File.Exists(path))
    {
        Console.WriteLine(".nugetguard.json already exists.");
        return 0;
    }

    PolicyFile.WriteDefault(dir);
    Console.WriteLine("Created .nugetguard.json with default settings.");
    return 0;
}

static async Task<int> HandleScan(string[] args)
{
    string? projectPath = null;
    var format = "console";
    string? output = null;
    string? failOn = null;
    var noTyposquat = false;
    var noLicense = false;
    var noVersion = false;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--format" when i + 1 < args.Length:
                format = args[++i];
                break;
            case "--output" when i + 1 < args.Length:
                output = args[++i];
                break;
            case "--fail-on" when i + 1 < args.Length:
                failOn = args[++i];
                break;
            case "--no-typosquat":
                noTyposquat = true;
                break;
            case "--no-license-check":
                noLicense = true;
                break;
            case "--no-version-check":
                noVersion = true;
                break;
            default:
                if (!args[i].StartsWith('-'))
                    projectPath = args[i];
                break;
        }
    }

    projectPath ??= Directory.GetCurrentDirectory();

    if (!Directory.Exists(projectPath) && !File.Exists(projectPath))
    {
        Console.Error.WriteLine($"Error: path '{projectPath}' does not exist.");
        return 2;
    }

    var policy = PolicyFile.Load(projectPath);
    if (failOn is not null)
        policy.FailOn = failOn;

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NuGetGuard/0.1.0");

    var engine = new ScanEngine(
        new CompositePackageResolver(),
        new VulnerabilityScanner(httpClient),
        new TyposquatDetector(),
        new LicenseChangeDetector(),
        new VersionInfoService(httpClient)
    );

    try
    {
        var result = await engine.ScanAsync(new ScanOptions
        {
            ProjectPath = projectPath,
            Policy = policy,
            CheckTyposquat = !noTyposquat,
            CheckLicenseChanges = !noLicense,
            CheckLatestVersion = !noVersion
        });

        IReportWriter writer = format.ToLowerInvariant() switch
        {
            "json" => new JsonReportWriter(),
            "sarif" => new SarifReportWriter(),
            _ => new ConsoleReportWriter()
        };

        if (output is not null)
        {
            await using var fileWriter = new StreamWriter(output);
            writer.Write(result, fileWriter);
            Console.WriteLine($"Report written to {output}");
        }
        else
        {
            writer.Write(result, Console.Out);
        }

        var threshold = policy.FailOnSeverity;
        var hasBlockingVulns = result.Vulnerabilities.Any(v =>
            SeverityExtensions.Parse(v.Severity) >= threshold && threshold != Severity.None);

        return hasBlockingVulns ? 1 : 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 2;
    }
}
