using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class ReportDownloadService : IReportDownloadService
    {
        private readonly HttpClient _httpClient;
        private readonly IWebDbAuthenticationService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReportDownloadService> _logger;

        public ReportDownloadService(
            HttpClient httpClient,
            IWebDbAuthenticationService authService,
            IConfiguration configuration,
            ILogger<ReportDownloadService> logger)
        {
            _httpClient = httpClient;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<byte[]> DownloadProviderCheckTotalsAsync(ReportParameters parameters)
        {
            try
            {
                // Ensure authenticated as Administrator
                if (!await _authService.IsAuthenticatedAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please log in first.");
                }

                var baseUrl = _configuration["WebDb:BaseUrl"] ?? "https://localhost:44356";
                var reportPath = _configuration["WebDb:ReportPath"] ?? "/Reports/Internal/ProviderCheckTotals.aspx";

                // Build URL with query parameters
                var queryParams = new Dictionary<string, string>
                {
                    ["AdviceDate"] = parameters.AdviceDate.ToString("M/d/yyyy"),
                    ["VendorNum"] = parameters.VendorNum,
                    ["Format"] = "PDF"  // Triggers RenderReport method
                };

                var queryString = string.Join("&", queryParams.Select(kvp =>
                    $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));
                var url = $"{baseUrl}{reportPath}?{queryString}";

                _logger.LogInformation($"Downloading report: {url}");

                // GET request to download report
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Report download failed: {response.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"Report download failed: {response.StatusCode}");
                }

                // Return PDF bytes
                var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                _logger.LogInformation($"Successfully downloaded report ({pdfBytes.Length} bytes)");

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading Provider Check Totals report");
                throw;
            }
        }

        public async Task<List<DateTime>> GetAvailableAdviceDatesAsync()
        {
            try
            {
                // TODO: Implement scraping of available dates from WebDB dropdown
                // For now, return sample dates
                var dates = new List<DateTime>();
                var currentDate = DateTime.Now.Date;

                for (int i = 0; i < 90; i += 7)
                {
                    dates.Add(currentDate.AddDays(-i));
                }

                _logger.LogInformation($"Retrieved {dates.Count} available advice dates");
                return await Task.FromResult(dates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available advice dates");
                return new List<DateTime>();
            }
        }

        public async Task<List<string>> GetVendorNumbersAsync()
        {
            try
            {
                // TODO: Implement scraping of vendor numbers from WebDB
                // For now, return sample vendor numbers
                var vendorNumbers = new List<string> { "1001", "1002", "1003", "2001", "2002" };

                _logger.LogInformation($"Retrieved {vendorNumbers.Count} vendor numbers");
                return await Task.FromResult(vendorNumbers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving vendor numbers");
                return new List<string>();
            }
        }
    }
}
