using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IRemittanceDownloadService
    {
        /// <summary>
        /// Downloads remittance advice PDFs from the Optum provider portal
        /// </summary>
        /// <param name="username">Optum portal username</param>
        /// <param name="password">Optum portal password</param>
        /// <param name="startDate">Date to search for remittances (format: MM/dd/yyyy)</param>
        /// <param name="downloadPath">Path where PDFs will be downloaded</param>
        /// <param name="progressCallback">Callback for progress updates</param>
        /// <returns>List of downloaded file paths</returns>
        Task<List<string>> DownloadRemittanceAdvicesAsync(
            string username,
            string password,
            string startDate,
            string downloadPath,
            Action<string> progressCallback = null);

        /// <summary>
        /// Gets the last 3 Mondays as reference dates
        /// </summary>
        /// <returns>List of dates in MM/dd/yyyy format</returns>
        List<string> GetLastThreeMondays();
    }
}
