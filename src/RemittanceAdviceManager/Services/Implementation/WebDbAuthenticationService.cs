using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class WebDbAuthenticationService : IWebDbAuthenticationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebDbAuthenticationService> _logger;
        private readonly CookieContainer _cookieContainer;
        private HttpClientHandler? _handler;
        private string? _authCookie;

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
                _logger.LogInformation("Fetching login page to get ViewState");
                var getResponse = await client.GetAsync(loginUrl);
                var loginPageHtml = await getResponse.Content.ReadAsStringAsync();

                // Step 2: Parse ViewState using AngleSharp
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(loginPageHtml);

                var viewState = document.QuerySelector("#__VIEWSTATE")?.GetAttribute("value");
                var viewStateGenerator = document.QuerySelector("#__VIEWSTATEGENERATOR")?.GetAttribute("value");
                var eventValidation = document.QuerySelector("#__EVENTVALIDATION")?.GetAttribute("value");

                _logger.LogInformation("Retrieved ViewState fields");

                // Step 3: Build POST data matching ASP.NET Web Forms expectations
                var postData = new Dictionary<string, string>
                {
                    ["__VIEWSTATE"] = viewState ?? "",
                    ["__VIEWSTATEGENERATOR"] = viewStateGenerator ?? "",
                    ["__EVENTVALIDATION"] = eventValidation ?? "",
                    ["ctl00$PagePlaceHolder$UserName"] = credentials.Username,
                    ["ctl00$PagePlaceHolder$Password"] = credentials.Password,
                    ["ctl00$PagePlaceHolder$LoginButton"] = "Log In"
                };

                if (credentials.RememberMe)
                {
                    postData["ctl00$PagePlaceHolder$RememberMe"] = "on";
                }

                // Step 4: POST credentials
                _logger.LogInformation($"Attempting login for user: {credentials.Username}");
                var content = new FormUrlEncodedContent(postData);
                var postResponse = await client.PostAsync(loginUrl, content);

                // Step 5: Check for authentication cookie (.ASPXAUTH)
                var cookies = _cookieContainer.GetCookies(new Uri(baseUrl));
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
                    _logger.LogWarning($"Authentication failed for user: {credentials.Username}");
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
            _logger.LogInformation("User logged out");
            await Task.CompletedTask;
        }

        public string? GetAuthCookie()
        {
            return _authCookie;
        }
    }
}
