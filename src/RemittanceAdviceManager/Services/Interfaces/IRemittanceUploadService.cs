using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RemittanceAdviceManager.Models;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IRemittanceUploadService
    {
        Task<UploadResult> UploadRemittanceAsync(RemittanceFile file, bool overwrite = false);
        Task<List<UploadResult>> UploadMultipleAsync(
            List<RemittanceFile> files,
            bool overwrite = false,
            IProgress<int>? progress = null);
    }
}
