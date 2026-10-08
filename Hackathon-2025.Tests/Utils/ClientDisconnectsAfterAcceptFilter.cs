using Microsoft.AspNetCore.Mvc.Filters;

namespace Hackathon_2025.Tests.Utils;

/// <summary>
/// Simulates a client that disconnects right after its request was accepted: every CancellationToken
/// action argument is replaced with an already-cancelled token (after model binding, so the request still runs).
/// </summary>
internal sealed class ClientDisconnectsAfterAcceptFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var key in context.ActionArguments.Where(a => a.Value is CancellationToken).Select(a => a.Key).ToList())
            context.ActionArguments[key] = new CancellationToken(canceled: true);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
