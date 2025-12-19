using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RemittanceAdviceManager.Data;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class FileTrackingService : IFileTrackingService
    {
        private readonly AppDbContext _dbContext;

        public FileTrackingService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<RemittanceFile>> GetDownloadedFilesAsync()
        {
            return await _dbContext.RemittanceFiles
                .Where(f => f.Status == FileStatus.Downloaded)
                .OrderByDescending(f => f.DownloadedDate)
                .ToListAsync();
        }

        public async Task<List<RemittanceFile>> GetAlteredFilesAsync()
        {
            return await _dbContext.RemittanceFiles
                .Where(f => f.Status == FileStatus.Altered)
                .OrderByDescending(f => f.DownloadedDate)
                .ToListAsync();
        }

        public async Task<List<RemittanceFile>> GetAllFilesAsync()
        {
            return await _dbContext.RemittanceFiles
                .OrderByDescending(f => f.DownloadedDate)
                .ToListAsync();
        }

        public async Task<RemittanceFile> AddFileAsync(RemittanceFile file)
        {
            _dbContext.RemittanceFiles.Add(file);
            await _dbContext.SaveChangesAsync();
            return file;
        }

        public async Task UpdateFileStatusAsync(int fileId, FileStatus status)
        {
            var file = await _dbContext.RemittanceFiles.FindAsync(fileId);
            if (file != null)
            {
                file.Status = status;
                if (status == FileStatus.Uploaded)
                {
                    file.UploadedDate = DateTime.Now;
                }
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task MarkAsPrintedAsync(int fileId)
        {
            var file = await _dbContext.RemittanceFiles.FindAsync(fileId);
            if (file != null)
            {
                file.IsPrinted = true;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<RemittanceFile?> GetFileByIdAsync(int fileId)
        {
            return await _dbContext.RemittanceFiles.FindAsync(fileId);
        }

        public async Task DeleteFileAsync(int fileId)
        {
            var file = await _dbContext.RemittanceFiles.FindAsync(fileId);
            if (file != null)
            {
                _dbContext.RemittanceFiles.Remove(file);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
