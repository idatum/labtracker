using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using System.Collections.Concurrent;

namespace LabTracker.Mqtt;

using LabTracker;

/// <summary>
/// MQTT implementation that reads current published client states from retained messages.
/// </summary>
public class MqttPublishedReader : IPublished
{
    private readonly ILogger<MqttPublishedReader> _logger;
    private readonly Options _options;

    /// <summary>
    /// Whether this implementation forces a complete snapshot of all clients.
    /// </summary>
    public bool ForceSnapshot => false;

    /// <summary>
    /// Initializes a new instance of the MqttPublishedReader class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="options">Configuration options containing MQTT broker settings</param>
    public MqttPublishedReader(ILogger<MqttPublishedReader> logger, IOptions<Options> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Reads current client states from MQTT retained messages by subscribing to the configured topic pattern.
    /// </summary>
    /// <returns>A dictionary with client state keys and their corresponding connection states</returns>
    public async Task<Dictionary<string, ClientState>> ReadCurrentStatesAsync()
    {
        var clientStates = new ConcurrentDictionary<string, ClientState>();
        IMqttClient? mqttClient = null;

        try
        {
            var factory = new MqttClientFactory();
            mqttClient = factory.CreateMqttClient();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Mqtt.BrokerHost, _options.Mqtt.BrokerPort)
                .WithCleanSession()
                .WithClientId($"labtracker-reader-{Environment.MachineName}-{Guid.NewGuid():N}");

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

            var clientOptions = optionsBuilder.Build();

            _logger.LogInformation("Connecting to MQTT broker to read retained client states at {host}:{port}",
                _options.Mqtt.BrokerHost, _options.Mqtt.BrokerPort);

            await mqttClient.ConnectAsync(clientOptions);

            // Subscribe to appropriate topic pattern based on configuration
            var subscriptionTopic = _options.Mqtt.IncludeApInTopic
                ? $"{_options.Mqtt.TopicPrefix}/+/+"  // topic/ap/client
                : $"{_options.Mqtt.TopicPrefix}/+";   // topic/client

            // Set up message handler to collect retained messages
            var messageCollectionComplete = new TaskCompletionSource<bool>();
            var timeoutTimer = new System.Timers.Timer(1000);

            timeoutTimer.Elapsed += (_, _) =>
            {
                timeoutTimer.Stop();
                messageCollectionComplete.TrySetResult(true);
            };

            mqttClient.ApplicationMessageReceivedAsync += (e) =>
            {
                try
                {
                    // Only process retained messages
                    if (!e.ApplicationMessage.Retain)
                    {
                        _logger.LogDebug("Skipping non-retained message on topic: {topic}", e.ApplicationMessage.Topic);
                        return Task.CompletedTask;
                    }

                    var topic = e.ApplicationMessage.Topic;
                    var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;
                    var timestamp = DateTime.UtcNow;

                    _logger.LogDebug("Processing retained message on topic: {topic}, payload: {payload}", topic, payload);

                    // Parse the topic to extract client ID and AP hostname
                    var (clientId, apHostname) = ParseTopic(topic);

                    // Use aggregated AP name when not including AP in topic
                    if (!_options.Mqtt.IncludeApInTopic)
                    {
                        apHostname = Options.AllApsAggregate;
                    }

                    // Process if we have a valid client ID, and either AP hostname (when required) or no AP required
                    if (!string.IsNullOrEmpty(clientId) && 
                        (_options.Mqtt.IncludeApInTopic ? !string.IsNullOrEmpty(apHostname) : true))
                    {
                        // Determine connection state based on payload
                        var isConnected = IsConnectedPayload(payload);

                        // Create client state
                        var key = PublishedUtils.CreateClientStateKey(clientId, apHostname);
                        var clientState = new ClientState
                        {
                            ClientId = clientId,
                            ApHostname = apHostname,
                            IsConnected = isConnected,
                            LastUpdated = timestamp,
                            LastPayload = payload
                        };

                        clientStates.AddOrUpdate(key, clientState, (_, _) => clientState);

                        _logger.LogDebug("Added client state: {clientId} @ {ap} = {connected}",
                            clientId, apHostname ?? "global", isConnected);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing retained message on topic: {topic}", e.ApplicationMessage.Topic);
                }
                return Task.CompletedTask;
            };

            await mqttClient.SubscribeAsync(subscriptionTopic, MqttQualityOfServiceLevel.AtLeastOnce);

            _logger.LogInformation("Subscribed to {topic}, waiting for retained messages...", subscriptionTopic);

            // Start the timeout timer
            timeoutTimer.Start();

            // Wait for message collection to complete
            await messageCollectionComplete.Task;

            _logger.LogInformation("Finished reading retained messages. Found {count} client states", clientStates.Count);

            return clientStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read current client states from MQTT");
            return [];
        }
        finally
        {
            if (mqttClient != null)
            {
                try
                {
                    if (mqttClient.IsConnected)
                    {
                        await mqttClient.DisconnectAsync();
                    }
                    mqttClient.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing MQTT client");
                }
            }
        }
    }

    private (string? clientId, string? apHostname) ParseTopic(string topic)
    {
        try
        {
            var parts = topic.Split('/');

            if (_options.Mqtt.IncludeApInTopic)
            {
                // Format: {prefix}/{ap}/{client}
                if (parts.Length >= 3 && parts[0] == _options.Mqtt.TopicPrefix)
                {
                    return (parts[2], parts[1]);
                }
            }
            else
            {
                // Format: {prefix}/{client}
                if (parts.Length >= 2 && parts[0] == _options.Mqtt.TopicPrefix)
                {
                    return (parts[1], null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MQTT topic: {topic}", topic);
        }

        return (null, null);
    }

    private bool IsConnectedPayload(string payload)
    {
        return string.Equals(payload, _options.Mqtt.ConnectedPayload, StringComparison.OrdinalIgnoreCase);
    }
}