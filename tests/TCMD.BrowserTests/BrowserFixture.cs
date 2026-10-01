using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;

namespace TCMD.BrowserTests;

public sealed class BrowserFixture : IAsyncLifetime
{
    public const string AdminUserName = "browser-admin";
    public const string AdminPassword = "Password1";
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif
    private Process? api;
    private readonly ConcurrentQueue<string> apiOutput = new();
    public string BaseUrl { get; private set; } = string.Empty;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var root = FindRepositoryRoot();
        var connection = Environment.GetEnvironmentVariable("TCMD_BROWSER_TEST_CONNECTION_STRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=TCMD.BrowserTests;Trusted_Connection=True;TrustServerCertificate=True";
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "TCMD.BrowserTests", StringComparison.Ordinal))
            throw new InvalidOperationException("Browser tests require the dedicated TCMD.BrowserTests database.");

        await RunAsync("dotnet", $"ef database drop --configuration {BuildConfiguration} --force --no-build --project src/TCMD.Infrastructure --startup-project src/TCMD.Api", root, connection);
        await RunAsync("dotnet", $"ef database update --configuration {BuildConfiguration} --no-build --project src/TCMD.Infrastructure --startup-project src/TCMD.Api", root, connection);
        BaseUrl = $"https://127.0.0.1:{GetAvailablePort()}";
        var start = new ProcessStartInfo("dotnet",
            $"run --configuration {BuildConfiguration} --no-build --no-launch-profile --project src/TCMD.Api --urls {BaseUrl}")
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Development", ["ConnectionStrings__TCMD"] = connection,
                ["BootstrapAdmin__Enabled"] = "true", ["BootstrapAdmin__UserName"] = AdminUserName,
                ["BootstrapAdmin__DisplayName"] = "Browser Administrator", ["BootstrapAdmin__Password"] = AdminPassword }
        };
        api = Process.Start(start) ?? throw new InvalidOperationException("Could not start TCMD.Api for browser tests.");
        api.OutputDataReceived += (_, eventArgs) => CaptureApiOutput(eventArgs.Data);
        api.ErrorDataReceived += (_, eventArgs) => CaptureApiOutput(eventArgs.Data);
        api.BeginOutputReadLine();
        api.BeginErrorReadLine();

        try
        {
            await WaitUntilReadyAsync();
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(new() { Headless = true });
        }
        catch
        {
            if (Browser is not null) await Browser.DisposeAsync();
            Playwright?.Dispose();
            await StopApiAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
        await StopApiAsync();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "TCMD.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private async Task WaitUntilReadyAsync()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler);
        Exception? lastError = null;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (api is null || api.HasExited)
                throw StartupFailure("TCMD.Api exited before becoming ready.");
            try
            {
                if ((await http.GetAsync($"{BaseUrl}/health")).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException exception)
            {
                lastError = exception;
            }
            await Task.Delay(500);
        }
        throw StartupFailure($"TCMD.Api did not become ready at {BaseUrl}.", lastError);
    }

    private void CaptureApiOutput(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line)) apiOutput.Enqueue(line);
    }

    private InvalidOperationException StartupFailure(string message, Exception? inner = null) =>
        new($"{message}{Environment.NewLine}API output:{Environment.NewLine}{string.Join(Environment.NewLine, apiOutput)}", inner);

    private async Task StopApiAsync()
    {
        if (api is null) return;
        if (!api.HasExited)
        {
            api.Kill(true);
            await api.WaitForExitAsync();
        }
        api.Dispose();
        api = null;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task RunAsync(string file, string arguments, string cwd, string connection)
    {
        var start = new ProcessStartInfo(file, arguments) { WorkingDirectory=cwd, UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true };
        start.Environment["ConnectionStrings__TCMD"] = connection;
        using var process = Process.Start(start)!;
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
        if(process.ExitCode!=0)throw new InvalidOperationException($"{file} {arguments} failed.\n{await stdout}\n{await stderr}");
    }
}

[CollectionDefinition(Name)]
public sealed class BrowserCollection : ICollectionFixture<BrowserFixture> { public const string Name = "Browser"; }
