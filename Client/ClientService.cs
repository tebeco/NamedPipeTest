using Client.IPC;

namespace Client;

public sealed class ClientService : BackgroundService
{
    private readonly ILogger<ClientService> _logger;

    public ClientService(ILogger<ClientService> logger)
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
            PipeClient client = new("NamedPipeTest", _logger);
            client.DataReceived += (sender, args) =>
            {
                _logger.LogInformation("Received - Title: {Title}, Message: {Message}", args.Title, args.Message);
            };

            var clientTask = client.StartAsync(stoppingToken);

            var sendingTask = Task.Run(async () =>
            {
                int count = 0;
                while (true)
                {
                    await client.SendAsync("Title1", $"Message > {count}{Environment.NewLine}", stoppingToken);
                    count++;
                }
            });

            await Task.WhenAll(clientTask, sendingTask);
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