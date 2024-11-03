using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSystem.Messaging
{
    public interface IRabbitMQPublisher<T>
    {
        Task PublishMessageAsync(T message, string queueName);
    }
}
