using MediatR;
using Microsoft.Extensions.Logging;

namespace AIAPP.Backend.Shared.Cqrs;

/// <summary>
/// CQRS 管道只记录请求类型和结果，不记录请求对象内容；命令里可能带手机号、验证码或 token。
/// </summary>
public sealed class RequestLoggingBehavior<TRequest, TResponse>(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling CQRS request {RequestName}", requestName);
        var response = await next().ConfigureAwait(false);
        logger.LogInformation("Handled CQRS request {RequestName}", requestName);
        return response;
    }
}
