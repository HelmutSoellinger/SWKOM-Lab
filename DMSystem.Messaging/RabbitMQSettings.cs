public class RabbitMQSettings
{
    public string HostName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Dictionary<string, string> Queues { get; set; } = new Dictionary<string, string>();
}
