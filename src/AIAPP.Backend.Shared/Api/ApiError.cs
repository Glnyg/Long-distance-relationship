using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AIAPP.Observability;

namespace AIAPP.Backend.Shared.Api;

/// <summary>
/// 统一 API 错误。错误码稳定给客户端和排障使用，中文提示给用户展示；不要把异常堆栈或隐私原文放进这里。
/// </summary>
public sealed record ApiError(string ErrorCode, string Message, int StatusCode)
{
    public static ApiError BadRequest(string errorCode, string message)
    {
        return new ApiError(errorCode, message, StatusCodes.Status400BadRequest);
    }

    public static ApiError Unauthorized(string errorCode, string message)
    {
        return new ApiError(errorCode, message, StatusCodes.Status401Unauthorized);
    }

    public static ApiError Forbidden(string errorCode, string message)
    {
        return new ApiError(errorCode, message, StatusCodes.Status403Forbidden);
    }

    public static ApiError NotFound(string errorCode, string message)
    {
        return new ApiError(errorCode, message, StatusCodes.Status404NotFound);
    }

    public static ApiError Conflict(string errorCode, string message)
    {
        return new ApiError(errorCode, message, StatusCodes.Status409Conflict);
    }

    public ProblemDetails ToProblemDetails(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = new ProblemDetails
        {
            Status = StatusCode,
            Title = ErrorCode,
            Detail = Message
        };

        problem.Extensions["error_code"] = ErrorCode;
        problem.Extensions["message"] = Message;
        problem.Extensions["correlation_id"] = GetCorrelationId(httpContext);
        return problem;
    }

    public IResult ToResult(HttpContext httpContext)
    {
        return Results.Problem(ToProblemDetails(httpContext));
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(AiAppTelemetryNames.CorrelationIdItemName, out var value)
            && value is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        if (httpContext.Response.Headers.TryGetValue(AiAppTelemetryNames.CorrelationIdHeaderName, out var headerValue))
        {
            return headerValue.ToString();
        }

        return string.Empty;
    }
}
