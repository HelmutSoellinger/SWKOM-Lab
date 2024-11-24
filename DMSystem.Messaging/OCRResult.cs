using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSystem.Messaging
{
    public class OCRResult
    {
        public string DocumentId { get; set; } = string.Empty;
        public string OcrText { get; set; } = string.Empty;
    }
}
