using LabTracker;

var builder = Host.CreateApplicationBuilder(args);

// Add additional configuration files
builder.Configuration.AddJsonFile("appsettings.Options.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.Options.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Configuration sources (in order of precedence):
// 1. Command line arguments
// 2. Environment variables  
// 3. appsettings.{Environment}.json
// 4. appsettings.json
//
// Environment variable format: SectionName__PropertyName
// For arrays use: SectionName__ArrayProperty__Index
// For nested sections use: SectionName__SubSection__PropertyName
// Examples:
//   Options__Unifi__AccessPoints__0=192.168.1.100
//   Options__Unifi__AccessPoints__1=192.168.1.101
//   Options__Unifi__PrivateKeyPath=/path/to/key
//   Options__DelayMs=30000
//   Options__Mqtt__BrokerHost=mqtt.example.com
//   Options__Mqtt__BrokerPort=1883
//   Options__UnifiApi__Key=your-api-key

// Configure options with environment variable override support
builder.Services.Configure<Options>(
    builder.Configuration.GetSection(Options.SectionName));

// Register the publisher service
// To use console publisher for testing, change MqttPublisher to ConsolePublisher
builder.Services.AddSingleton<IPublisher, MqttPublisher>();

// Register the UniFi API client
builder.Services.AddSingleton<IUniFiApiClient, UniFiApiClient>();

// Register the published state reader service based on InitialState configuration
var options = builder.Configuration.GetSection(Options.SectionName).Get<Options>() ?? new Options();
switch (options.InitialState)
{
    case InitialState.UnifiAPI:
        builder.Services.AddSingleton<IPublished, UnifiPublishedReader>();
        break;
    case InitialState.MQTT:
        builder.Services.AddSingleton<IPublished, MqttPublishedReader>();
        break;
    case InitialState.None:
        // Don't initialize client states - use null implementation
        builder.Services.AddSingleton<IPublished, NullPublishedReader>();
        break;
    default:
        builder.Services.AddSingleton<IPublished, MqttPublishedReader>();
        break;
}

// Register the client information provider
builder.Services.AddSingleton<IClientInfoProvider, SshClientProvider>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
