# LabTracker

.NET background service that monitors UniFi Access Point client connections and publishes events to MQTT. Designed for home automation like Home Assistant to track device presence.

Python version [here](https://github.com/idatum/unifi_tracker).

## Summary

- Periodic monitoring of UniFi Access Points using SSH key auth
- MQTT integration with retained messages for home automation
- Initialization from MQTT retained state or UniFi API

## Configuration

Create `appsettings.Options.json`:

```json
{
  "Options": {
    "DelayMs": 15000,
    "InitialState": "MQTT",
    "MaxIdleTimeSeconds": 0,
    
    "Unifi": {
      "AccessPoints": ["192.168.1.100", "192.168.1.101"],
      "Username": "admin", 
      "PrivateKeyPath": "/path/to/ssh/private/key"
    },
    
    "UnifiApi": {
      "UseHttps": true,
      "IgnoreSSLErrors": false,
      "BaseUrl": "your-unifi-controller:8443",
      "Key": "your-unifi-api-key",
      "PageSize": 100
    },
    
    "Mqtt": {
      "BrokerHost": "your-mqtt.net",
      "BrokerPort": 8883,
      "Username": "labtracker",
      "Password": "your-mqtt-password",
      "TopicPrefix": "labtracker",
      "IncludeApInTopic": true,
      "UseTls": true,
      "Retain": true,
      "UseHostname": false,
      "ConnectedPayload": "home",
      "DisconnectedPayload": "not_home"
    }
  }
}
```

### Environment Variables

Use double underscores (`__`) for nested properties:

```bash
# UniFi Access Points
Options__Unifi__AccessPoints__0=192.168.1.100
Options__Unifi__AccessPoints__1=192.168.1.101
Options__Unifi__Username=admin
Options__Unifi__PrivateKeyPath=/path/to/key

# MQTT Settings
Options__Mqtt__BrokerHost=mqtt.example.com
Options__Mqtt__BrokerPort=8883
Options__Mqtt__Username=labtracker
Options__Mqtt__Password=your-password
Options__Mqtt__TopicPrefix=labtracker
Options__Mqtt__UseTls=true

# UniFi API (optional)
Options__UnifiApi__BaseUrl=unifi.example.com:443
Options__UnifiApi__Key=your-api-key
Options__UnifiApi__UseHttps=true

# General Settings
Options__DelayMs=15000
Options__InitialState=MQTT
```

### Podman/Docker

```bash
# Clone the repository
git clone <repository-url>
cd labtracker

# Build the container
podman build -t labtracker .

# Run with configuration
podman run -d \
  -v /path/to/ssh/keys:/app/.ssh:ro \
  -e Options__Unifi__AccessPoints__0=192.168.1.100 \
  -e Options__Unifi__AccessPoints__1=192.168.1.101 \
  -e Options__Unifi__Username=admin \
  -e Options__Unifi__PrivateKeyPath=/app/.ssh/id_rsa \
  -e Options__Mqtt__BrokerHost=mqtt.example.com \
  -e Options__Mqtt__Username=labtracker \
  -e Options__Mqtt__Password=your-password \
  --name labtracker \
  labtracker
```

### Core Settings

| Option | Description | Default |
|--------|-------------|---------|
| `DelayMs` | Polling interval in milliseconds | `60000` |
| `InitialState` | How to initialize client states (`MQTT`, `UnifiAPI`, `None`) | `MQTT` |
| `MaxIdleTimeSeconds` | Filter out clients idle longer than this (0 = disabled) | `0` |
| `ConnectionTimeoutSeconds` | SSH connection timeout | `5` |
| `CommandTimeoutSeconds` | SSH command execution timeout | `15` |

### UniFi Access Points (`Unifi` section)

| Option | Description | Default |
|--------|-------------|----------|
| `AccessPoints` | Array of AP IP addresses or hostnames | - |
| `Username` | SSH username for access points | admin |
| `PrivateKeyPath` | Path to SSH private key file | - |

### UniFi API (`UnifiApi` section)

| Option | Description | Default |
|--------|-------------|---------|
| `BaseUrl` | UniFi Controller URL | - |
| `Key` | UniFi API key | - |
| `UseHttps` | Use HTTPS for API calls | `true` |
| `IgnoreSSLErrors` | Ignore SSL certificate errors | `false` |
| `PageSize` | API pagination size | `100` |

### MQTT (`Mqtt` section)

| Option | Description | Default |
|--------|-------------|---------|
| `BrokerHost` | MQTT broker hostname | `localhost` |
| `BrokerPort` | MQTT broker port | `1883` |
| `Username` | MQTT username | - |
| `Password` | MQTT password | - |
| `TopicPrefix` | Base topic prefix | `labtracker` |
| `IncludeApInTopic` | Include AP name in topic path | `true` |
| `UseHostname` | Use device hostname as client ID | `false` |
| `UseTls` | Enable TLS encryption | `false` |
| `Retain` | Set retain flag on messages | `false` |
| `ConnectedPayload` | Message payload for connected clients | `home` |
| `DisconnectedPayload` | Message payload for disconnected clients | `not_home` |

## Initial State Options

- **`MQTT`**: Read current client states from MQTT retained messages (recommended)
- **`UnifiAPI`**: Query UniFi Controller API for current client states
- **`None`**: Start with empty state, only track changes from startup

## Details

- **Worker**: Main background service with polling loop
- **IClientInfoProvider**: Interface for retrieving client information
- **IPublisher**: Interface for publishing events
- **IPublished**: Interface for reading current state (MQTT/UniFi API/Null implementations)
- **Options**: Hierarchical configuration system with environment variable support

## Unit tests
In LabTracker.Tests, run `dotnet test`