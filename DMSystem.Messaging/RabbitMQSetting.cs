namespace DMSystem.Messaging
{
    public class RabbitMQSetting
    {
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty; // Add QueueName
    }

    public static class RabbitMQQueues
    {
        public const string OrderValidationQueue = "orderValidationQueue";
    }
}
