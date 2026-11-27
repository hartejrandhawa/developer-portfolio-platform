using System.Security.Claims;

namespace DevPortfolio.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The app's user id from our own JWT. Only call this behind [Authorize]: a
    /// missing or malformed claim there means a bad token, so we fail loudly
    /// rather than silently acting as user 0.
    /// </summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid user id claim.");
    }
}
