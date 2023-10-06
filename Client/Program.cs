using Client;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<ClientService>();

var host = builder.Build();
host.Run();
