using System.Collections.Generic;
using System.Threading.Tasks;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IPrintService
    {
        Task PrintPdfAsync(string pdfPath);
        Task PrintMultiplePdfsAsync(List<string> pdfPaths, bool showDialog = true);
    }
}
