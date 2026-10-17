using System.Security.Claims;

namespace MudBlazorDemo.Middleware;

/// <summary>
/// Middleware to read authentication information from OpenID Auth Proxy headers.
/// The proxy (e.g., Azure AD Application Proxy) handles authentication and passes user info via headers.
/// </summary>
public class ProxyAuthMiddleware
{
    private readonly RequestDelegate _next;

    public ProxyAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Read headers provided by the OpenID Auth Proxy
        var remoteName = context.Request.Headers["X-Remote-User"].ToString();
        var remoteEmail = context.Request.Headers["X-Remote-Email"].ToString();
        var remoteDisplayName = context.Request.Headers["X-Remote-Name"].ToString();

        // If proxy headers are present, create a claims principal
        if (!string.IsNullOrEmpty(remoteName))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, remoteName),
                new Claim(ClaimTypes.Name, remoteName),
            };

            if (!string.IsNullOrEmpty(remoteEmail))
            {
                claims.Add(new Claim(ClaimTypes.Email, remoteEmail));
            }

            if (!string.IsNullOrEmpty(remoteDisplayName))
            {
                claims.Add(new Claim(ClaimTypes.GivenName, remoteDisplayName));
            }

            var identity = new ClaimsIdentity(claims, "ProxyAuth");
            var principal = new ClaimsPrincipal(identity);

            context.User = principal;
        }

        await _next(context);
    }
}
