using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RemittanceAdviceManager.Models;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IReportDownloadService
    {
        Task<byte[]> DownloadProviderCheckTotalsAsync(ReportParameters parameters);
        Task<List<DateTime>> GetAvailableAdviceDatesAsync();
        Task<List<string>> GetVendorNumbersAsync();
    }
}
