using System.Collections.Generic;
using System.Threading.Tasks;
using RemittanceAdviceManager.Models;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IFileTrackingService
    {
        Task<List<RemittanceFile>> GetDownloadedFilesAsync();
        Task<List<RemittanceFile>> GetAlteredFilesAsync();
        Task<List<RemittanceFile>> GetAllFilesAsync();
        Task<RemittanceFile> AddFileAsync(RemittanceFile file);
        Task UpdateFileStatusAsync(int fileId, FileStatus status);
        Task MarkAsPrintedAsync(int fileId);
        Task<RemittanceFile?> GetFileByIdAsync(int fileId);
        Task DeleteFileAsync(int fileId);
    }
}
