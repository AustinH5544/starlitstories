using System.Text.Json;

namespace Hackathon_2025.Tests.Utils;

internal static class ServerErrors
{
    /// <summary>
    /// The app has no exception-handling middleware, so in TestServer an unhandled exception surfaces
    /// as an exception from HttpClient instead of a 500 response. Accept either.
    /// </summary>
    public static async Task AssertServerErrorAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            var resp = await send();
            Assert.IsTrue((int)resp.StatusCode >= 500, $"Expected a server error but got {(int)resp.StatusCode}.");
        }
        catch (Exception ex) when (ex is not AssertFailedException)
        {
            // Unhandled server exception propagated by TestServer: expected.
        }
    }
}

internal static class Eventually
{
    public static async Task AssertAsync(Func<Task<bool>> condition, string because, int timeoutMs = 15000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        Assert.Fail($"Condition not met within {timeoutMs}ms: {because}");
    }
}

internal static class JsonHelpers
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage resp)
    {
        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }
}
