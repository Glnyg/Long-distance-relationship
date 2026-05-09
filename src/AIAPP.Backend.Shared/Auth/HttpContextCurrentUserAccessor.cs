using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AIAPP.Backend.Shared.Auth;

public interface ICurrentUserAccessor
{
    bool TryGetUserId(out Guid userId);
}

/// <summary>
/// 从 HTTP 鉴权声明读取当前用户。不要从请求体或自定义 Header 信任用户 ID，避免越权访问情侣数据。
/// </summary>
public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public bool TryGetUserId(out Guid userId)
    {
        userId = default;
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
