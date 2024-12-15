using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DMSystem.Messaging
{
    public interface IModelWrapper : IDisposable
    {
        IBasicProperties CreateBasicProperties();
        void BasicPublish(string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body);
        void BasicAck(ulong deliveryTag, bool multiple);
        void BasicNack(ulong deliveryTag, bool multiple, bool requeue);
        string BasicConsume(string queue, bool autoAck, AsyncEventingBasicConsumer consumer);
        void BasicQos(uint prefetchSize, ushort prefetchCount, bool global);
        void QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object>? arguments);
        IModel GetInnerModel(); // For accessing underlying RabbitMQ IModel if necessary
    }

    public class ModelWrapper : IModelWrapper
    {
        private readonly IModel _innerModel;

        public ModelWrapper(IModel innerModel)
        {
            _innerModel = innerModel ?? throw new ArgumentNullException(nameof(innerModel));
        }

        public IBasicProperties CreateBasicProperties() => _innerModel.CreateBasicProperties();

        public void BasicPublish(string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body) =>
            _innerModel.BasicPublish(exchange, routingKey, basicProperties, body);

        public void BasicAck(ulong deliveryTag, bool multiple) =>
            _innerModel.BasicAck(deliveryTag, multiple);

        public void BasicNack(ulong deliveryTag, bool multiple, bool requeue) =>
            _innerModel.BasicNack(deliveryTag, multiple, requeue);

        public string BasicConsume(string queue, bool autoAck, AsyncEventingBasicConsumer consumer)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            return _innerModel.BasicConsume(queue, autoAck, consumer);
        }

        public void BasicQos(uint prefetchSize, ushort prefetchCount, bool global) =>
            _innerModel.BasicQos(prefetchSize, prefetchCount, global);

        public void QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object>? arguments) =>
            _innerModel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);

        public IModel GetInnerModel() => _innerModel;

        public void Dispose() => _innerModel.Dispose();
    }
}