using Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<ServerService>();

var host = builder.Build();
host.Run();
