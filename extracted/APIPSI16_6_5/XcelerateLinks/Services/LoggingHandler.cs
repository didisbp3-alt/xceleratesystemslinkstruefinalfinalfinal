using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XcelerateLinks.Mvc.Http
{
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHandler> _logger;

        public LoggingHandler(ILogger<LoggingHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                // Log request line + headers
                _logger.LogInformation("Outgoing HTTP request: {Method} {Uri}", request.Method, request.RequestUri);
                foreach (var h in request.Headers)
                    _logger.LogDebug("Req-Header: {Name}: {Value}", h.Key, string.Join(", ", h.Value));

                if (request.Content != null)
                {
                    foreach (var h in request.Content.Headers)
                        _logger.LogDebug("Req-Content-Header: {Name}: {Value}", h.Key, string.Join(", ", h.Value));

                    var reqBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Request body: {Body}", string.IsNullOrWhiteSpace(reqBody) ? "(empty)" : reqBody);
                }

                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Log response status + headers
                _logger.LogInformation("Received HTTP response: {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);
                foreach (var h in response.Headers)
                    _logger.LogDebug("Resp-Header: {Name}: {Value}", h.Key, string.Join(", ", h.Value));
                if (response.Content != null)
                {
                    foreach (var h in response.Content.Headers)
                        _logger.LogDebug("Resp-Content-Header: {Name}: {Value}", h.Key, string.Join(", ", h.Value));

                    var respBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    // Keep response body safe-sized in logs - still print fully for now as we need full diagnostics
                    _logger.LogInformation("Response body (raw): {Body}", string.IsNullOrWhiteSpace(respBody) ? "(empty)" : respBody);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while logging HTTP request/response");
                throw;
            }
        }
    }
}