using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Glarbot;

var builder = Host.CreateApplicationBuilder();
builder.Logging.AddConsole();
builder.Services.AddHostedService<Glarbot.Glarbot>();
builder.Services.AddOptions<Settings>()
    .Bind(builder.Configuration.GetSection(nameof(Settings)))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<GoogleSettings>()
    .Bind(builder.Configuration.GetSection(nameof(GoogleSettings)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Build().Run();
