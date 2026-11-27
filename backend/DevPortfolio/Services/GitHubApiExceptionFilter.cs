using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevPortfolio.Services;

/// <summary>
/// GitHub being down or rate-limited is an upstream problem, not a bug in our API:
/// return 502 with a readable message instead of a generic 500.
/// </summary>
public class GitHubApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not GitHubApiException ex) return;
        context.Result = new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status502BadGateway };
        context.ExceptionHandled = true;
    }
}
