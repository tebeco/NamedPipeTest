using Server.IPC;

namespace Server;

public sealed class ServerService : BackgroundService
{
    private readonly ILogger<ServerService> _logger;

    public ServerService(ILogger<ServerService> logger)
    {
        _logger = logger;
    }

    public void Stop()
    {
        _logger.LogInformation("Closing server pipe.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                PipeServer server = new("NamedPipeTest", _logger);
                server.DataReceived += (sender, args) => _logger.LogInformation("Received - Title: {Title}, Message: {Message}", args.Title, args.Message);

                var runningTask = server.RunAsync(stoppingToken);

                var sendingTask = Task.Run(async () =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await server.SendAsync("Title2", "Message2", stoppingToken);
                        await Task.Delay(1000, stoppingToken);
                    }
                });

                await Task.WhenAll(runningTask, sendingTask);
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);

            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}