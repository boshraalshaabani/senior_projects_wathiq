using System.Net;
using System.Text.Json;
using eArchiveSystem.Application.Exceptions;

namespace eArchiveSystem.Presentation.Middleware
{
    public class GlobalExceptionHandlingMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

        public GlobalExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                await HandleExceptionAsync(context, exception);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while processing request {Path}", context.Request.Path);

            var statusCode = exception switch
            {
                ApiException apiException => (int)apiException.StatusCode,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var response = new
            {
                message = exception is ApiException ? exception.Message : "An unexpected error occurred.",
                statusCode,
                traceId = context.TraceIdentifier
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
        }
    }
}
