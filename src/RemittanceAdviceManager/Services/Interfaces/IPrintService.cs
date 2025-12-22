using System.Collections.Generic;
using System.Threading.Tasks;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IPrintService
    {
        Task<bool> PrintPdfAsync(string pdfPath, bool showDialog = true);
        Task<int> PrintMultiplePdfsAsync(List<string> pdfPaths, bool showDialog = false);
    }
}
