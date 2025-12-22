using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class WebDbAuthenticationService : IWebDbAuthenticationService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebDbAuthenticationService> _logger;
        private readonly CookieContainer _cookieContainer;
        private HttpClientHandler? _handler;
        private string? _authCookie;
        private IWebDriver? _driver;
        private bool _disposed = false;

        public event EventHandler<bool>? AuthenticationStateChanged;

        public WebDbAuthenticationService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<WebDbAuthenticationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _cookieContainer = new CookieContainer();
        }

        public async Task<bool> AuthenticateAsync(WebDbCredentials credentials)
        {
            try
            {
                // Use Selenium for authentication (same approach as Optum download)
                var baseUrl = _configuration["WebDb:BaseUrl"] ?? "https://10.0.0.51/db";
                var loginPath = _configuration["WebDb:LoginPath"] ?? "/Login.aspx";
                var loginUrl = $"{baseUrl}{loginPath}";

                _logger.LogInformation($"Authenticating to {loginUrl} using Selenium");

                // Set up Chrome driver in headless mode
                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArgument("--headless=new");
                chromeOptions.AddArgument("--disable-gpu");
                chromeOptions.AddArgument("--no-sandbox");

                new DriverManager().SetUpDriver(new ChromeConfig());

                // Create ChromeDriverService and hide the command prompt window
                var chromeDriverService = ChromeDriverService.CreateDefaultService();
                chromeDriverService.HideCommandPromptWindow = true;

                _driver = new ChromeDriver(chromeDriverService, chromeOptions);

                // Navigate to login page
                _driver.Navigate().GoToUrl(loginUrl);
                await Task.Delay(2000); // Wait for page to load

                // Find and fill login fields
                _logger.LogInformation("Finding login form elements...");
                var usernameField = _driver.FindElement(By.Id("ctl00_PagePlaceHolder_Login1_UserName"));
                var passwordField = _driver.FindElement(By.Id("ctl00_PagePlaceHolder_Login1_Password"));
                var loginButton = _driver.FindElement(By.Id("ctl00_PagePlaceHolder_Login1_LoginButton"));

                _logger.LogInformation($"Entering username: {credentials.Username}");
                usernameField.Click(); // Click first to focus
                await Task.Delay(200);
                usernameField.Clear();
                usernameField.SendKeys(credentials.Username);

                _logger.LogInformation($"Entering password... (length: {credentials.Password?.Length ?? 0})");

                if (string.IsNullOrEmpty(credentials.Password))
                {
                    _logger.LogError("Password is null or empty!");
                }
                else
                {
                    // Use JavaScript to set password value and trigger events
                    var jsExecutor = (IJavaScriptExecutor)_driver;
                    var escapedPassword = credentials.Password.Replace("\\", "\\\\").Replace("'", "\\'");

                    // Set value and trigger input event
                    jsExecutor.ExecuteScript(@"
                        arguments[0].value = arguments[1];
                        arguments[0].dispatchEvent(new Event('input', { bubbles: true }));
                        arguments[0].dispatchEvent(new Event('change', { bubbles: true }));
                    ", passwordField, credentials.Password);

                    // Verify password was entered
                    var passwordValue = passwordField.GetAttribute("value");
                    _logger.LogInformation($"Password field has {passwordValue?.Length ?? 0} characters after setting");
                }

                // Click login
                loginButton.Click();
                await Task.Delay(3000); // Wait for login to complete

                // Check if we're still on the login page or if we got redirected
                var currentUrl = _driver.Url;
                var isAuthenticated = !currentUrl.Contains("Login.aspx");

                if (isAuthenticated)
                {
                    _logger.LogInformation($"Successfully authenticated user: {credentials.Username}");
                    _logger.LogInformation($"Redirected to: {currentUrl}");

                    // Get cookies from Selenium and store the auth cookie
                    var cookies = _driver.Manage().Cookies.AllCookies;
                    var aspxAuthCookie = cookies.FirstOrDefault(c => c.Name == ".ASPXAUTH");
                    if (aspxAuthCookie != null)
                    {
                        _authCookie = aspxAuthCookie.Value;
                        _logger.LogInformation("Captured .ASPXAUTH cookie");
                    }

                    // Notify subscribers that authentication state changed
                    AuthenticationStateChanged?.Invoke(this, true);
                }
                else
                {
                    _logger.LogWarning($"Authentication failed for user: {credentials.Username} - still on login page");

                    // Check for error message
                    try
                    {
                        var errorElement = _driver.FindElement(By.Id("ctl00_PagePlaceHolder_Login1_FailureText"));
                        if (!string.IsNullOrEmpty(errorElement.Text))
                        {
                            _logger.LogWarning($"Login error message: {errorElement.Text}");
                        }
                    }
                    catch { }

                    // Notify subscribers that authentication failed
                    AuthenticationStateChanged?.Invoke(this, false);
                }

                return isAuthenticated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Selenium authentication");
                return false;
            }
            finally
            {
                // Keep driver open for subsequent requests, or close it
                // For now, we'll keep it open
            }
        }

        public async Task<bool> AuthenticateAsync_Old_HttpClient(WebDbCredentials credentials)
        {
            try
            {
                // Create handler with cookie container
                _handler = new HttpClientHandler
                {
                    CookieContainer = _cookieContainer,
                    UseCookies = true,
                    AllowAutoRedirect = true
                };

                // Create new HttpClient with handler for this session
                using var client = new HttpClient(_handler);
                var baseUrl = _configuration["WebDb:BaseUrl"] ?? "https://localhost:44356";
                var loginPath = _configuration["WebDb:LoginPath"] ?? "/Login.aspx";
                var loginUrl = $"{baseUrl}{loginPath}";

                client.BaseAddress = new Uri(baseUrl);

                // Step 1: GET login page to retrieve ViewState
                _logger.LogInformation($"Fetching login page from: {loginUrl}");
                var getResponse = await client.GetAsync(loginUrl);
                _logger.LogInformation($"GET Response Status: {getResponse.StatusCode}");

                var loginPageHtml = await getResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Received HTML page, length: {loginPageHtml.Length} characters");

                // Step 2: Parse ViewState using AngleSharp
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(loginPageHtml);

                var viewState = document.QuerySelector("#__VIEWSTATE")?.GetAttribute("value");
                var viewStateGenerator = document.QuerySelector("#__VIEWSTATEGENERATOR")?.GetAttribute("value");
                var eventValidation = document.QuerySelector("#__EVENTVALIDATION")?.GetAttribute("value");

                _logger.LogInformation($"ViewState found: {viewState != null}, Generator found: {viewStateGenerator != null}, EventValidation found: {eventValidation != null}");

                // Log all input fields to help diagnose field name issues
                var inputs = document.QuerySelectorAll("input");
                _logger.LogInformation($"Found {inputs.Length} input fields on login page:");
                foreach (var input in inputs)
                {
                    var name = input.GetAttribute("name");
                    var type = input.GetAttribute("type");
                    var id = input.GetAttribute("id");
                    if (!string.IsNullOrEmpty(name))
                    {
                        _logger.LogInformation($"  Input: name='{name}', type='{type}', id='{id}'");
                    }
                }

                // Step 3: Build POST data matching ASP.NET Web Forms expectations
                var postData = new Dictionary<string, string>
                {
                    ["__VIEWSTATE"] = viewState ?? "",
                    ["__VIEWSTATEGENERATOR"] = viewStateGenerator ?? "",
                    ["__EVENTVALIDATION"] = eventValidation ?? "",
                    ["ctl00$PagePlaceHolder$Login1$UserName"] = credentials.Username,
                    ["ctl00$PagePlaceHolder$Login1$Password"] = credentials.Password,
                    ["ctl00$PagePlaceHolder$Login1$LoginButton"] = "Log In"
                };

                // Step 4: POST credentials
                _logger.LogInformation($"Attempting login for user: {credentials.Username}");
                _logger.LogInformation($"Submitting to URL: {loginUrl}");
                _logger.LogInformation($"POST data fields:");
                foreach (var kvp in postData.Where(k => !k.Key.StartsWith("__")))
                {
                    var displayValue = kvp.Key.Contains("Password") ? "***HIDDEN***" : kvp.Value;
                    _logger.LogInformation($"  {kvp.Key} = {displayValue}");
                }

                var content = new FormUrlEncodedContent(postData);
                var postResponse = await client.PostAsync(loginUrl, content);
                _logger.LogInformation($"POST Response Status: {postResponse.StatusCode}");

                // Check the response content for error messages
                var postResponseHtml = await postResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"POST Response length: {postResponseHtml.Length} characters");

                // Save response HTML to file for debugging
                try
                {
                    var debugPath = Path.Combine(Path.GetTempPath(), "webdb_login_response.html");
                    await File.WriteAllTextAsync(debugPath, postResponseHtml);
                    _logger.LogInformation($"Saved POST response HTML to: {debugPath}");
                }
                catch { }

                // Check if we were redirected or if there's an error message on the page
                var postResponseDoc = await parser.ParseDocumentAsync(postResponseHtml);

                // Look for common error elements
                var errorLabel = postResponseDoc.QuerySelector("#ctl00_PagePlaceHolder_Login1_FailureText");
                var errorSpan = postResponseDoc.QuerySelector(".failureNotification");
                var validationSummary = postResponseDoc.QuerySelector(".validation-summary-errors");

                if (errorLabel != null && !string.IsNullOrEmpty(errorLabel.TextContent.Trim()))
                {
                    _logger.LogWarning($"Login error (FailureText): {errorLabel.TextContent.Trim()}");
                }
                if (errorSpan != null && !string.IsNullOrEmpty(errorSpan.TextContent.Trim()))
                {
                    _logger.LogWarning($"Login error (failureNotification): {errorSpan.TextContent.Trim()}");
                }
                if (validationSummary != null && !string.IsNullOrEmpty(validationSummary.TextContent.Trim()))
                {
                    _logger.LogWarning($"Login error (validation): {validationSummary.TextContent.Trim()}");
                }

                // Check if the response contains the login form (means we didn't get redirected)
                var stillHasLoginForm = postResponseDoc.QuerySelector("#ctl00_PagePlaceHolder_Login1_UserName") != null;
                _logger.LogInformation($"Still on login page after POST: {stillHasLoginForm}");

                // Step 5: Check for authentication cookie (.ASPXAUTH)
                var cookies = _cookieContainer.GetCookies(new Uri(baseUrl));
                _logger.LogInformation($"Cookies received: {cookies.Count}");

                foreach (System.Net.Cookie cookie in cookies)
                {
                    _logger.LogInformation($"Cookie: {cookie.Name} = {cookie.Value.Substring(0, Math.Min(20, cookie.Value.Length))}...");
                }

                _authCookie = cookies[".ASPXAUTH"]?.Value;

                var isAuthenticated = !string.IsNullOrEmpty(_authCookie);

                if (isAuthenticated)
                {
                    _logger.LogInformation($"Successfully authenticated user: {credentials.Username}");

                    // Copy cookies to main HttpClient
                    // Note: This is a simplified approach. In production, you might want to
                    // use a shared cookie container across all HttpClient instances
                }
                else
                {
                    _logger.LogWarning($"Authentication failed for user: {credentials.Username} - .ASPXAUTH cookie not found");
                }

                return isAuthenticated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication");
                return false;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            return await Task.FromResult(!string.IsNullOrEmpty(_authCookie));
        }

        public async Task LogoutAsync()
        {
            _authCookie = null;

            if (_driver != null)
            {
                try
                {
                    _driver.Quit();
                    _driver = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing Selenium driver");
                }
            }

            _logger.LogInformation("User logged out");

            // Notify subscribers that user logged out
            AuthenticationStateChanged?.Invoke(this, false);

            await Task.CompletedTask;
        }

        public string? GetAuthCookie()
        {
            return _authCookie;
        }

        public async Task<byte[]> NavigateAndDownloadAsync(string url)
        {
            try
            {
                if (_driver == null)
                {
                    throw new InvalidOperationException("Selenium driver not initialized. Please authenticate first.");
                }

                _logger.LogInformation($"Downloading via Selenium: {url}");

                // Use fetch API to download the PDF asynchronously
                var script = @"
                    var callback = arguments[arguments.length - 1];
                    var url = arguments[0];

                    fetch(url)
                        .then(response => response.blob())
                        .then(blob => {
                            var reader = new FileReader();
                            reader.onloadend = function() {
                                var base64data = reader.result.split(',')[1];
                                callback(base64data);
                            };
                            reader.onerror = function() {
                                callback(null);
                            };
                            reader.readAsDataURL(blob);
                        })
                        .catch(error => {
                            console.error('Fetch error:', error);
                            callback(null);
                        });
                ";

                var base64Data = ((IJavaScriptExecutor)_driver).ExecuteAsyncScript(script, url) as string;

                if (string.IsNullOrEmpty(base64Data))
                {
                    throw new Exception("Failed to download PDF - no data received");
                }

                var pdfBytes = Convert.FromBase64String(base64Data);
                _logger.LogInformation($"Downloaded {pdfBytes.Length} bytes via fetch");

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading from: {url}");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Cleanup managed resources
                    if (_driver != null)
                    {
                        try
                        {
                            _logger.LogInformation("Disposing Selenium driver...");
                            _driver.Quit();
                            _driver.Dispose();
                            _driver = null;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error disposing Selenium driver");
                        }
                    }

                    _handler?.Dispose();
                }

                _disposed = true;
            }
        }

        ~WebDbAuthenticationService()
        {
            Dispose(false);
        }
    }
}
