using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class RemittanceUploadService : IRemittanceUploadService
    {
        private readonly HttpClient _httpClient;
        private readonly IWebDbAuthenticationService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RemittanceUploadService> _logger;

        public RemittanceUploadService(
            HttpClient httpClient,
            IWebDbAuthenticationService authService,
            IConfiguration configuration,
            ILogger<RemittanceUploadService> logger)
        {
            _httpClient = httpClient;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<UploadResult> UploadRemittanceAsync(RemittanceFile file, bool overwrite = false)
        {
            try
            {
                // Ensure authenticated
                if (!await _authService.IsAuthenticatedAsync())
                {
                    _logger.LogError("Not authenticated to WebDB");
                    return new UploadResult
                    {
                        Success = false,
                        Message = "Not authenticated. Please log in first.",
                        FileName = file.FileName
                    };
                }

                var baseUrl = _configuration["WebDb:BaseUrl"] ?? "https://localhost:44356";
                var uploadPath = _configuration["WebDb:UploadPath"] ?? "/Remittances/UploadAdvice.aspx";
                var uploadUrl = $"{baseUrl}{uploadPath}";

                // Step 1: GET upload page to obtain ViewState
                _logger.LogInformation($"Fetching upload page for ViewState");
                var getResponse = await _httpClient.GetAsync(uploadUrl);
                var pageHtml = await getResponse.Content.ReadAsStringAsync();

                // Step 2: Parse ViewState
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(pageHtml);

                var viewState = document.QuerySelector("#__VIEWSTATE")?.GetAttribute("value");
                var viewStateGenerator = document.QuerySelector("#__VIEWSTATEGENERATOR")?.GetAttribute("value");
                var eventValidation = document.QuerySelector("#__EVENTVALIDATION")?.GetAttribute("value");

                // Step 3: Create multipart form data
                using var formData = new MultipartFormDataContent();

                // Add hidden fields
                formData.Add(new StringContent(viewState ?? ""), "__VIEWSTATE");
                formData.Add(new StringContent(viewStateGenerator ?? ""), "__VIEWSTATEGENERATOR");
                formData.Add(new StringContent(eventValidation ?? ""), "__EVENTVALIDATION");

                // Add file
                if (!File.Exists(file.FilePath))
                {
                    _logger.LogError($"File not found: {file.FilePath}");
                    return new UploadResult
                    {
                        Success = false,
                        Message = $"File not found: {file.FilePath}",
                        FileName = file.FileName
                    };
                }

                var fileBytes = await File.ReadAllBytesAsync(file.FilePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                formData.Add(fileContent, "ctl00$PagePlaceHolder$RemittanceUpload", file.FileName);

                // Add checkboxes
                if (overwrite)
                {
                    formData.Add(new StringContent("on"), "ctl00$PagePlaceHolder$OverwriteCheckBox");
                }

                // Add submit button
                formData.Add(new StringContent("Upload"), "ctl00$PagePlaceHolder$UploadButton");

                // Step 4: POST upload
                _logger.LogInformation($"Uploading file: {file.FileName}");
                var response = await _httpClient.PostAsync(uploadUrl, formData);
                var responseHtml = await response.Content.ReadAsStringAsync();

                // Step 5: Parse response for success/error messages
                var resultDoc = await parser.ParseDocumentAsync(responseHtml);

                // Look for status messages in the response
                var statusElement = resultDoc.QuerySelector(".status, .message, #statusLabel");
                var statusText = statusElement?.TextContent?.Trim() ?? "";

                var success = response.IsSuccessStatusCode &&
                             !statusText.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                             !statusText.Contains("failed", StringComparison.OrdinalIgnoreCase);

                _logger.LogInformation($"Upload result for {file.FileName}: {(success ? "Success" : "Failed")}");

                return new UploadResult
                {
                    Success = success,
                    Message = statusText.Length > 0 ? statusText : (success ? "File uploaded successfully" : "Upload failed"),
                    FileName = file.FileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading file: {file.FileName}");
                return new UploadResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    FileName = file.FileName
                };
            }
        }

        public async Task<List<UploadResult>> UploadMultipleAsync(
            List<RemittanceFile> files,
            bool overwrite = false,
            IProgress<int>? progress = null)
        {
            var results = new List<UploadResult>();
            int completedCount = 0;

            foreach (var file in files)
            {
                var result = await UploadRemittanceAsync(file, overwrite);
                results.Add(result);

                completedCount++;
                progress?.Report((completedCount * 100) / files.Count);

                // Small delay between uploads
                await Task.Delay(500);
            }

            return results;
        }
    }
}
