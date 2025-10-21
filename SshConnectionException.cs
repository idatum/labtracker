namespace LabTracker;

/// <summary>
/// Exception thrown when SSH connection to UniFi Access Point fails
/// </summary>
public class SshConnectionException : Exception
{
    public SshConnectionException(string message) : base(message)
    {
    }

    public SshConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}