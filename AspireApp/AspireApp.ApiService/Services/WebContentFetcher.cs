using Microsoft.Playwright;

namespace AspireApp.ApiService.Services;

public interface IWebContentFetcher
{
    Task<string> GetHtmlContentAsync(string url, CancellationToken cancellationToken);
}

public class WebContentFetcher : IWebContentFetcher
{
    public async Task<string> GetHtmlContentAsync(string url, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true // false for debugging
        });

        var page = await browser.NewPageAsync();
        var response = await page.GotoAsync(url, new PageGotoOptions
        {
            //WaitUntil = WaitUntilState.NetworkIdle,
            //Timeout = cancellationToken.IsCancellationRequested ? 1 : 30000 // 30 seconds default
        });
        
        await page.WaitForTimeoutAsync(1000);
        var finalHtmlContent = await page.ContentAsync();

        return response is { Ok: false } ? throw new Exception("shit went wrong") : finalHtmlContent;
    }
}