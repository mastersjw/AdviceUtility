using System.Threading.Tasks;
using RemittanceAdviceManager.Models;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IWebDbAuthenticationService
    {
        Task<bool> AuthenticateAsync(WebDbCredentials credentials);
        Task<bool> IsAuthenticatedAsync();
        Task LogoutAsync();
        string? GetAuthCookie();
    }
}
