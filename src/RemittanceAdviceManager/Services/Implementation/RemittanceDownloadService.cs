using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using RemittanceAdviceManager.Services.Interfaces;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class RemittanceDownloadService : IRemittanceDownloadService
    {
        private readonly ILogger<RemittanceDownloadService> _logger;
        private readonly IPdfProcessingService _pdfProcessingService;
        private IWebDriver _driver;
        private string _downloadPath;

        public RemittanceDownloadService(
            ILogger<RemittanceDownloadService> logger,
            IPdfProcessingService pdfProcessingService)
        {
            _logger = logger;
            _pdfProcessingService = pdfProcessingService;
        }

        public List<string> GetLastThreeMondays()
        {
            var mondays = new List<string>();
            var today = DateTime.Today;

            // Calculate days since last Monday
            int daysSinceMonday = ((int)today.DayOfWeek - 1);
            if (daysSinceMonday < 0)
                daysSinceMonday += 7;

            var lastMonday = today.AddDays(-daysSinceMonday);

            for (int i = 0; i < 3; i++)
            {
                mondays.Add(lastMonday.ToString("MM/dd/yyyy"));
                lastMonday = lastMonday.AddDays(-7);
            }

            return mondays;
        }

        public async Task<List<string>> DownloadRemittanceAdvicesAsync(
            string username,
            string password,
            string startDate,
            string downloadPath,
            Action<string> progressCallback = null)
        {
            var downloadedFiles = new List<string>();
            _downloadPath = downloadPath;

            try
            {
                progressCallback?.Invoke("Initializing Chrome WebDriver...");
                InitializeDriver(downloadPath);

                progressCallback?.Invoke("Navigating to Optum provider portal...");
                await LoginToPortalAsync(username, password, progressCallback);

                progressCallback?.Invoke("Searching for remittance advice files...");
                await SearchRemittanceAdvicesAsync(startDate, progressCallback);

                progressCallback?.Invoke("Downloading and processing files...");
                downloadedFiles = await ProcessTableAsync(progressCallback);

                progressCallback?.Invoke($"Download complete! {downloadedFiles.Count} files processed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during remittance download process");
                progressCallback?.Invoke($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                CleanupDriver();
            }

            return downloadedFiles;
        }

        private void InitializeDriver(string downloadPath)
        {
            try
            {
                var chromeOptions = new ChromeOptions();
                chromeOptions.AddUserProfilePreference("download.default_directory", downloadPath);
                chromeOptions.AddUserProfilePreference("plugins.always_open_pdf_externally", true);
                chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
                chromeOptions.AddUserProfilePreference("profile.default_content_settings.popups", 0);

                // Set up Chrome driver automatically
                new DriverManager().SetUpDriver(new ChromeConfig());

                _driver = new ChromeDriver(chromeOptions);
                _logger.LogInformation("Chrome WebDriver initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Chrome WebDriver");
                throw;
            }
        }

        private async Task LoginToPortalAsync(string username, string password, Action<string> progressCallback)
        {
            try
            {
                // Navigate to Optum provider portal
                _driver.Navigate().GoToUrl("https://provider-mt-mms.optum.com/tpa-ap-web/?navDeepDive=MT_publicProviderHomeDefaultContentMenu");
                await Task.Delay(3000);

                progressCallback?.Invoke("Clicking login link...");
                _driver.FindElement(By.Id("extLink_p912267537")).Click();
                await Task.Delay(9000);

                progressCallback?.Invoke("Entering credentials...");
                _driver.FindElement(By.Id("username")).SendKeys(username);
                _driver.FindElement(By.Id("login-pwd")).SendKeys(password);

                progressCallback?.Invoke("Logging in...");
                _driver.FindElement(By.Id("btnLogin")).Click();
                await Task.Delay(5000);

                _logger.LogInformation("Successfully logged in to Optum portal");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                throw new Exception("Failed to log in to Optum portal. Please check your credentials.", ex);
            }
        }

        private async Task SearchRemittanceAdvicesAsync(string startDate, Action<string> progressCallback)
        {
            try
            {
                // Navigate to remittance advice search
                progressCallback?.Invoke("Navigating to remittance search...");
                _driver.FindElement(By.Id("navLink_n1107658728")).Click();
                await Task.Delay(5000);

                progressCallback?.Invoke("Opening date search...");
                _driver.FindElement(By.Id("remitDateSearch")).Click();
                await Task.Delay(5000);

                // Set date range (from and to same date)
                progressCallback?.Invoke($"Setting search date to {startDate}...");
                _driver.FindElement(By.Id("remitFromDate")).Clear();
                _driver.FindElement(By.Id("remitFromDate")).SendKeys(startDate);
                _driver.FindElement(By.Id("thruDate")).Clear();
                _driver.FindElement(By.Id("thruDate")).SendKeys(startDate);

                progressCallback?.Invoke("Executing search...");
                _driver.FindElement(By.Id("dateAdvancedSearchButton")).Click();
                await Task.Delay(5000);

                // Set table to show 50 results per page
                new SelectElement(_driver.FindElement(By.Name("userBrowserTbl_length"))).SelectByValue("50");
                await Task.Delay(3000);

                _logger.LogInformation($"Search completed for date: {startDate}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during remittance search");
                throw new Exception("Failed to search for remittance advices.", ex);
            }
        }

        private async Task<List<string>> ProcessTableAsync(Action<string> progressCallback)
        {
            var downloadedFiles = new List<string>();

            try
            {
                // Find all rows in the results table
                ReadOnlyCollection<IWebElement> rows = _driver.FindElement(By.Id("userBrowserTbl"))
                    .FindElements(By.XPath(".//tbody/tr"));

                progressCallback?.Invoke($"Found {rows.Count} remittance advice files.");
                _logger.LogInformation($"Found {rows.Count} rows to process");

                int processedCount = 0;
                foreach (var row in rows)
                {
                    try
                    {
                        processedCount++;

                        // Extract data from table columns
                        string advNumber = row.FindElement(By.XPath("./td[1]")).Text.Trim();
                        string date = row.FindElement(By.XPath("./td[2]")).Text.Trim();
                        string providerId = row.FindElement(By.XPath("./td[3]")).Text.Trim().TrimStart('0');

                        string dateFormatted = date.Replace("/", "");
                        string newFileName = $"{dateFormatted}_{providerId}.pdf";

                        progressCallback?.Invoke($"[{processedCount}/{rows.Count}] Downloading {newFileName}...");

                        // Click download link
                        row.FindElement(By.XPath("./td[7]//a")).Click();
                        await Task.Delay(5000);

                        // Rename the downloaded file
                        string renamedPath = RenameDownloadedPDF(advNumber, newFileName);

                        if (!string.IsNullOrEmpty(renamedPath))
                        {
                            progressCallback?.Invoke($"[{processedCount}/{rows.Count}] Processing {newFileName}...");

                            // Extract "PAID CLAIM TOTALS" page
                            await _pdfProcessingService.ExtractPaidClaimsTotalsPageAsync(
                                renamedPath,
                                dateFormatted,
                                providerId);

                            downloadedFiles.Add(renamedPath);
                            _logger.LogInformation($"Successfully processed: {newFileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing row {processedCount}");
                        progressCallback?.Invoke($"Error processing row {processedCount}: {ex.Message}");
                    }
                }

                return downloadedFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing table");
                throw;
            }
        }

        private string RenameDownloadedPDF(string advNumber, string newFileName)
        {
            try
            {
                if (!Directory.Exists(_downloadPath))
                {
                    _logger.LogError($"Download directory does not exist: {_downloadPath}");
                    return null;
                }

                // Find file matching ADV number
                string[] matchingFiles = Directory.GetFiles(_downloadPath, $"{advNumber}*");

                if (matchingFiles.Length == 0)
                {
                    _logger.LogWarning($"No file found matching ADV Number: {advNumber}");
                    return null;
                }

                string oldPath = matchingFiles[0];

                if (!newFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    newFileName += ".pdf";
                }

                string newPath = Path.Combine(_downloadPath, newFileName);

                File.Move(oldPath, newPath, true);
                _logger.LogInformation($"Renamed file: {oldPath} -> {newPath}");

                return newPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error renaming file with ADV Number: {advNumber}");
                return null;
            }
        }

        private void CleanupDriver()
        {
            try
            {
                if (_driver != null)
                {
                    _driver.Quit();
                    _driver = null;
                    _logger.LogInformation("Chrome WebDriver closed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing Chrome WebDriver");
            }
        }
    }
}
