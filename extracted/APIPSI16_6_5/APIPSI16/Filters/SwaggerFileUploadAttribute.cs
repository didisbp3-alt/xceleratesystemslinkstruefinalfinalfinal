using System;

namespace APIPSI16.Filters
{
    /// <summary>
    /// Attribute to mark endpoints that accept file uploads.
    /// Used by FileUploadOperation filter to properly document file upload parameters in Swagger.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SwaggerFileUploadAttribute : Attribute
    {
    }
}
