using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSystem.OCR
{
    public class OcrProcessor
    {
        public string PerformOcr(byte[] pdfContent)
        {
            var tempPdfPath = Path.GetTempFileName() + ".pdf";
            File.WriteAllBytes(tempPdfPath, pdfContent);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tesseract",
                    Arguments = $"{tempPdfPath} stdout -l eng",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            File.Delete(tempPdfPath);
            return output;
        }
    }
}
