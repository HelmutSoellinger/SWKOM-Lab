public interface IRabbitMQService
{
    Task PublishMessageAsync<T>(T message, string queueName);
    void ConsumeQueue<T>(string queueName, Func<T, Task> onMessage);
}
