using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Supershow.Middlewares;

public class AuthSessionMiddleware
{
	private readonly RequestDelegate next;
	private readonly string loginPath;

	public AuthSessionMiddleware(RequestDelegate next, IOptionsMonitor<CookieAuthenticationOptions> options)
	{
		this.next = next;
		loginPath = options.Get("Cookies").LoginPath;
	}

	public async Task InvokeAsync(HttpContext context, DB db)
	{
		var endpoint = context.GetEndpoint();
		var requiresAuth = endpoint?.Metadata?.GetMetadata<IAuthorizeData>() != null;
		var logout = false;

		if (context.User.Identity?.IsAuthenticated == true)
		{
			var token = context.User.FindFirst("SessionToken")?.Value;
			var accountId = context.User.Identity.Name;
			var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

			if (token == null || accountId == null || role == null)
			{
				await context.SignOutAsync();
				logout = true;
			}
			else
			{
				var session = db.Sessions.FirstOrDefault(
					s => s.Token == token &&
					s.Device.AccountId.ToString() == accountId &&
					s.ExpiresAt > DateTime.Now &&
					s.Device.IsVerified == true
				);

				if (session == null)
				{
					await context.SignOutAsync();
					logout = true;
				}
				else
				{
					var acc = db.Accounts
						.Include(a => a.AccountType)
						.FirstOrDefault(a =>
							a.Id == int.Parse(accountId) &&
							a.AccountType.Name == role &&
							!a.IsDeleted
						);
					if (acc == null)
					{
						await context.SignOutAsync();
						logout = true;
					}
					else
					{
						context.Items["Account"] = acc;
					}
				}
			}
		}

		if (requiresAuth && logout)
		{
			if (context.Request.IsAjax())
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				await context.Response.WriteAsync("Invalid session");
			}
			else
			{
				context.Response.Redirect(loginPath);
			}
			return;
		}

		await next(context);
	}
}
