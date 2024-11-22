using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSystem.Messaging
{
    public class RabbitMQSetting
    {
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty; 
        public string Password { get; set; } = string.Empty;
    }

    public static class RabbitMQQueues
    {
        public const string OrderValidationQueue = "orderValidationQueue";
    }

}
