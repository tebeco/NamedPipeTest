using Client.IPC;

namespace Client;

public sealed class ClientService : BackgroundService
{
    private readonly ILogger<ClientService> _logger;

    public ClientService(ILogger<ClientService> logger)
    {
        _logger = logger;
    }

    public async Task Start()
    {
        await Task.Run(new Action(async () =>
        {
            PipeClient client = new("NamedPipeTest", _logger);
            client.DataReceived += (sender, args) =>
            {
                _logger.LogInformation("Received - Title: {Title}, Message: {Message}", args.Title, args.Message);
            };

            _ = client.Start();
            Thread thread = new(async () =>
            {
                int count = 0;
                while (true)
                {
                    await client.Send("Title1", $"Message > {count}{Environment.NewLine}");
                    count++;
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }));
    }

    public async Task Stop()
    {
        await Task.Run(new Action(() =>
        {
            _logger.LogInformation("Closing server pipe.");
        }));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Start();
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

    private void RunBackgroundThread(ThreadStart start, string thname, ThreadPriority tp)
    {
        _logger.LogInformation("{thname} > Starting background thread!", thname);
        Thread background = new(start)
        {
            IsBackground = true,
            Name = thname,
            Priority = tp
        };

        background.Start();

        _logger.LogInformation("{thname} > Started background thread!", thname);
    }
}