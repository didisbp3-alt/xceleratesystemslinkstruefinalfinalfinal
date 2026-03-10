using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APIPSI16.Filters
{
    /// <summary>
    /// Swagger operation filter to handle file upload endpoints.
    /// This filter resolves issues with IFormFile parameters in Swagger schema generation.
    /// </summary>
    public class FileUploadOperation : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Check if the endpoint has the SwaggerFileUpload attribute
            var hasFileUploadAttribute = context.MethodInfo.GetCustomAttributes(true)
                .Any(attr => attr.GetType().Name == nameof(SwaggerFileUploadAttribute));

            if (!hasFileUploadAttribute)
                return;

            // Check if any parameter is IFormFile
            var formFileParams = context.ApiDescription.ParameterDescriptions
                .Where(p => p.ModelMetadata?.ModelType == typeof(IFormFile))
                .ToList();

            if (!formFileParams.Any())
                return;

            // Clear existing parameters for file upload
            operation.Parameters?.Clear();

            // Build schema properties from actual form file parameters
            var properties = new Dictionary<string, OpenApiSchema>();
            var requiredFields = new HashSet<string>();

            foreach (var fileParam in formFileParams)
            {
                properties[fileParam.Name] = new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary",
                    Description = $"The {fileParam.Name} to upload"
                };
                requiredFields.Add(fileParam.Name);
            }

            // Set up request body for multipart/form-data
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = properties,
                            Required = requiredFields
                        }
                    }
                }
            };
        }
    }
}
