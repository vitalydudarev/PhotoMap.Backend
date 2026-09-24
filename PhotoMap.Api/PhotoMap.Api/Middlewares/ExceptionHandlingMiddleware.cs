using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Middlewares
{
    /// <summary>
    /// Turns the exceptions of a request into a problem details response with the status code they stand for.
    /// Unexpected exceptions are logged and answered with 500, their message is not passed on to the client.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // the client has gone, there is no one to answer
            }
            catch (Exception exception)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogError(exception, "Request failed after the response had started");
                    throw;
                }

                await WriteProblemAsync(context, exception);
            }
        }

        private Task WriteProblemAsync(HttpContext context, Exception exception)
        {
            var (statusCode, detail) = exception switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                NotAuthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
                PhotoSourceException { IsAuthError: true } => (StatusCodes.Status401Unauthorized, exception.Message),
                // the photo source failed to answer, not this application
                PhotoSourceException => (StatusCodes.Status502BadGateway, exception.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error has occurred.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Request failed");
            }
            else
            {
                _logger.LogWarning("Request failed with {StatusCode}: {Message}", statusCode, exception.Message);
            }

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = ReasonPhrases.GetReasonPhrase(statusCode),
                Detail = detail,
                Instance = context.Request.Path
            };

            context.Response.Clear();
            context.Response.StatusCode = statusCode;

            return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
