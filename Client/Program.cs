namespace Client;

public class Program
{
    private static Logger logger;

    public static Logger Logger
    { get { return logger ?? throw new Exception("Logger Is Null"); } set => logger = value; }

    private static readonly Version version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version("1.0");

    public static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "appsettings.json")
            .Build();

        logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            var userName = "N/A";
            if (OperatingSystem.IsWindows())
            {
                userName = WindowsIdentity.GetCurrent().Name;
            }

            logger.Information("Application Starts. Version: {version}, Account: [{userName}]", version, userName);

            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception e)
        {
            logger.Fatal(e, "Application terminated unexpectedly");
        }
        finally
        {
            logger.Information("Application terminated.");

            logger.Dispose();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
                    .UseWindowsService(options =>
                    {
                        options.ServiceName = "Client Service";
                    })
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true);
                        config.AddCommandLine(args);
                    })
                    .ConfigureLogging((context, logging) =>
                    {
                        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                        logging.AddSerilog(logger);
                    })
                    .ConfigureServices((hostContext, services) =>
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(services);
                        }

                        services.AddHostedService<ClientService>();
                    });
    }
}