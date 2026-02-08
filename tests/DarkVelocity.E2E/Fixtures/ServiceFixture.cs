using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CliWrap;
using DarkVelocity.E2E.Auth;

namespace DarkVelocity.E2E.Fixtures;

public sealed class ServiceFixture : IAsyncLifetime
{
    private CancellationTokenSource? _cts;
    private Task? _processTask;
    private readonly int _port = 5200;

    public string BaseUrl => $"http://localhost:{_port}";
    public HttpClient HttpClient { get; } = new();
    public SpiceDbTestHelper SpiceDb { get; } = new();

    public async Task InitializeAsync()
    {
        VerifyInfrastructure();

        _cts = new CancellationTokenSource();

        _processTask = Cli.Wrap("dotnet")
            .WithArguments(["run", "--project", "src/DarkVelocity.Host", "-c", "Release"])
            .WithWorkingDirectory(GetSolutionRoot())
            .WithEnvironmentVariables(env => env
                .Set("ASPNETCORE_ENVIRONMENT", "Development"))
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[HOST] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Debug.WriteLine($"[HOST:ERR] {line}")))
            .ExecuteAsync(_cts.Token)
            .Task;

        await WaitForHealthy(TimeSpan.FromSeconds(120));
        await SpiceDb.WriteSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            try
            {
                if (_processTask is not null)
                    await _processTask;
            }
            catch (OperationCanceledException) { }
            _cts.Dispose();
        }

        HttpClient.Dispose();
    }

    private async Task WaitForHealthy(TimeSpan timeout)
    {
        using var healthCts = new CancellationTokenSource(timeout);
        var healthUrl = $"{BaseUrl}/health";

        while (!healthCts.Token.IsCancellationRequested)
        {
            try
            {
                var response = await HttpClient.GetAsync(healthUrl, healthCts.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (HttpRequestException) { }

            await Task.Delay(500, healthCts.Token);
        }

        throw new TimeoutException(
            $"Service did not become healthy at {healthUrl} within {timeout.TotalSeconds}s");
    }

    private static void VerifyInfrastructure()
    {
        var requiredPorts = new (int Port, string Service)[]
        {
            (10002, "Azurite Table Storage"),
            (5432, "PostgreSQL"),
            (50051, "SpiceDB gRPC"),
        };

        var failures = new List<string>();
        foreach (var (port, service) in requiredPorts)
        {
            if (!IsPortOpen(port))
                failures.Add($"  - {service} on port {port}");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Required infrastructure is not running. Start it with: cd docker && docker compose up -d\n" +
                $"Missing services:\n{string.Join("\n", failures)}");
        }
    }

    private static bool IsPortOpen(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect("localhost", port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static string GetSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DarkVelocity.slnx")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find solution root (DarkVelocity.slnx). Run tests from the solution directory.");
    }
}
