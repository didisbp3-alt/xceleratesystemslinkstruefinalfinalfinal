using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace APIPSI16.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string folder);
        Task DeleteFileAsync(string fileUrl);
        bool ValidateImageFile(IFormFile file, out string errorMessage);
        bool ValidateDocumentOrImageFile(IFormFile file, out string errorMessage);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly long _maxFileSize = 5 * 1024 * 1024;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private readonly string[] _allowedDocumentExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };

        public FileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public bool ValidateImageFile(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (file == null || file.Length == 0)
            {
                errorMessage = "File is required";
                return false;
            }

            if (file.Length > _maxFileSize)
            {
                errorMessage = $"File size must not exceed {_maxFileSize / (1024 * 1024)} MB";
                return false;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedImageExtensions.Contains(extension))
            {
                errorMessage = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", _allowedImageExtensions)}";
                return false;
            }

            return true;
        }

        public bool ValidateDocumentOrImageFile(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (file == null || file.Length == 0)
            {
                errorMessage = "File is required";
                return false;
            }

            if (file.Length > _maxFileSize)
            {
                errorMessage = $"File size must not exceed {_maxFileSize / (1024 * 1024)} MB";
                return false;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedDocumentExtensions.Contains(extension))
            {
                errorMessage = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", _allowedDocumentExtensions)}";
                return false;
            }

            return true;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{folder}/{uniqueFileName}";
        }

        public async Task DeleteFileAsync(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return;

            var filePath = Path.Combine(_environment.WebRootPath, fileUrl.TrimStart('/'));
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
        }
    }
}
