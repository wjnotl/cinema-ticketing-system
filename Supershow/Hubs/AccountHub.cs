using Microsoft.AspNetCore.SignalR;

namespace Supershow.Hubs;

public class AccountHub : Hub
{
    public async Task Initialize()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            await Clients.Caller.SendAsync("Error", "No HTTP context available");
            return;
        }

        var account = httpContext.GetAccount();
        if (account == null)
        {
            return;
        }

        var token = httpContext.User.FindFirst("SessionToken")?.Value;
        if (token == null)
        {
            return;
        }

        await Clients.Caller.SendAsync("Initialize", account.Id, token);
    }
}