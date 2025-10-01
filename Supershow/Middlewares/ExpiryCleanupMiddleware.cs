namespace Supershow.Middlewares;

public class ExpiryCleanupMiddleware
{
    private RequestDelegate next;
    public ExpiryCleanupMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task Invoke(HttpContext context, ExpiryCleanupService expiryCleanupService)
    {
        await expiryCleanupService.Cleanup();
        await next(context);
    }
}