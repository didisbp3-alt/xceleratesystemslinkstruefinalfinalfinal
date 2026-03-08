using Microsoft.AspNetCore.Http;

namespace APIPSI16.Models.DTOs
{
    public class FileUploadDTO
    {
        public IFormFile File { get; set; } = null!;
    }

    public class FileUploadResponseDTO
    {
        public bool Success { get; set; }
        public string? FileUrl { get; set; }
        public string? Message { get; set; }
    }
}
