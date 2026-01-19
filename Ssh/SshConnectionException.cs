namespace LabTracker.Ssh;

/// <summary>
/// SSH connection unexpected response.
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