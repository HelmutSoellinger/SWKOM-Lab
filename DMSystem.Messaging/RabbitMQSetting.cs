using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSystem.Messaging
{
    public class RabbitMQSetting
    {
        public string HostName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public static class RabbitMQQueues
    {
        public const string OrderValidationQueue = "orderValidationQueue";
    }

}
