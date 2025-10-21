using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace LabTracker.Mqtt;

using LabTracker;

/// <summary>
/// MQTT implementation of the IPublisher interface.
/// </summary>
public class MqttPublisher : IPublisher, IAsyncDisposable
{
    private readonly ILogger<MqttPublisher> _logger;
    private readonly Options _options;
    private IMqttClient? _mqttClient;

    /// <summary>
    /// Initializes a new instance of the MqttPublisher class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="options">Configuration options containing MQTT broker settings</param>
    public MqttPublisher(ILogger<MqttPublisher> logger, IOptions<Options> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Gets a value indicating whether the MQTT client is currently connected to the broker.
    /// </summary>
    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    private static MqttApplicationMessage CreateMessage(string topic, string payload, bool retain)
    {
        return new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();
    }

    /// <summary>
    /// Initializes the MQTT client and establishes connection to the configured broker.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation</returns>
    public async Task InitializeAsync()
    {
        try
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();
            
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Mqtt.BrokerHost, _options.Mqtt.BrokerPort)
                .WithCleanSession();

            if (_options.Mqtt.UseTls)
            {
                optionsBuilder = optionsBuilder.WithTlsOptions(tlsOptions =>
                {
                    // Enable TLS with default settings
                });
            }

            if (!string.IsNullOrEmpty(_options.Mqtt.Username) && !string.IsNullOrEmpty(_options.Mqtt.Password))
            {
                optionsBuilder = optionsBuilder.WithCredentials(_options.Mqtt.Username, _options.Mqtt.Password);
            }

            var options = optionsBuilder.Build();
            
            _mqttClient.ConnectedAsync += (e) =>
            {
                _logger.LogInformation("MQTT client connected to {host}:{port}", _options.Mqtt.BrokerHost, _options.Mqtt.BrokerPort);
                return Task.CompletedTask;
            };

            _mqttClient.DisconnectedAsync += (e) =>
            {
                _logger.LogWarning("MQTT client disconnected. Reason: {reason}, Exception: {exception}", 
                    e.Reason, e.Exception?.Message);
                return Task.CompletedTask;
            };

            _logger.LogDebug("Attempting to connect to MQTT broker at {host}:{port} with TLS: {tls}", 
                _options.Mqtt.BrokerHost, _options.Mqtt.BrokerPort, _options.Mqtt.UseTls);
            
            await _mqttClient.ConnectAsync(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MQTT client");
            throw;
        }
    }

    /// <summary>
    /// Publishes client connection and disconnection events to MQTT topics.
    /// </summary>
    /// <param name="apHostname">Hostname of the access point where the client events occurred</param>
    /// <param name="connectedClients">List of client identifiers that connected</param>
    /// <param name="disconnectedClients">List of client identifiers that disconnected</param>
    /// <returns>A task representing the asynchronous publish operation</returns>
    public async Task PublishClientsAsync(string apHostname, List<string> connectedClients, List<string> disconnectedClients)
    {
        if (!IsConnected)
        {
            _logger.LogInformation("MQTT client not connected, skipping publish");
            return;
        }

        try
        {
            var topic = _options.Mqtt.IncludeApInTopic 
                ? $"{_options.Mqtt.TopicPrefix}/{apHostname}"
                : _options.Mqtt.TopicPrefix;
            // Publish connected clients
            foreach (var client in connectedClients)
            {
                var connectTopic = $"{topic}/{client}";
                var connectPayload = _options.Mqtt.ConnectedPayload;
                var connectMessage = CreateMessage(connectTopic, connectPayload, _options.Mqtt.Retain);
                await _mqttClient!.PublishAsync(connectMessage);
                _logger.LogDebug("Published {connectTopic} for AP {ap} to MQTT", connectTopic, apHostname);
            }
            // Publish disconnected clients
            foreach (var client in disconnectedClients)
            {
                var disconnectTopic = $"{topic}/{client}";
                var disconnectPayload = _options.Mqtt.DisconnectedPayload;
                var disconnectMessage = CreateMessage(disconnectTopic, disconnectPayload, _options.Mqtt.Retain);
                await _mqttClient!.PublishAsync(disconnectMessage);
                _logger.LogDebug("Published {disconnectTopic} for AP {ap} to MQTT", disconnectTopic, apHostname);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT client events for AP {ap}", apHostname);
        }
    }

    /// <summary>
    /// Asynchronously disposes of the MQTT client resources.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous disposal operation</returns>
    public async ValueTask DisposeAsync()
    {
        if (_mqttClient != null)
        {
            try
            {
                if (_mqttClient.IsConnected)
                {
                    await _mqttClient.DisconnectAsync();
                }
                _mqttClient.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MQTT client");
            }
        }
        
        GC.SuppressFinalize(this);
    }
}