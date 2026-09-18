using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoTrade.Trading.API;
using AutoTrade.Trading.API.Controllers;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Session;
using AutoTrade.Trading.Handlers;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IConfiguration configuration = builder.Configuration;

// Exactly one mediator for the whole module.
builder.Services.AddHikyaku(hikyaku => hikyaku.RegisterServicesFromAssembly(typeof(AutoTrade.Trading.Handlers.Module).Assembly));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTradingApi();
builder.Services.AddTradingHandlers(configuration);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services
  .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
  .AddCookie(options =>
  {
    options.Cookie.Name = "autotrade.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = false;
    options.Events = new CookieAuthenticationEvents
    {
      OnValidatePrincipal = ValidatePrincipalAsync
    };
  });

builder.Services.AddAuthorization();

string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
  if (allowedOrigins.Length > 0)
  {
    policy.WithOrigins(allowedOrigins).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
  }
}));

WebApplication app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

using (IServiceScope scope = app.Services.CreateScope())
{
  TradingDatabaseInitializer initializer = scope.ServiceProvider.GetRequiredService<TradingDatabaseInitializer>();
  await initializer.InitializeAsync(CancellationToken.None);
}

app.Run();

// The cookie is only a carrier: every request re-validates the server-side session so revocation,
// expiry and operator deactivation take effect immediately.
static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
{
  string operatorIdClaim = context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
  string sessionTokenClaim = context.Principal.FindFirst(SessionController.SessionTokenClaimType)?.Value;

  if (!Guid.TryParse(operatorIdClaim, out Guid operatorId) || string.IsNullOrWhiteSpace(sessionTokenClaim))
  {
    context.RejectPrincipal();
    return;
  }

  IHikyaku hikyaku = context.HttpContext.RequestServices.GetRequiredService<IHikyaku>();

  SessionDto session = await hikyaku.Send(new GetCurrentSession
  {
    OperatorId = operatorId,
    SessionToken = sessionTokenClaim
  }, context.HttpContext.RequestAborted);

  if (session == null)
  {
    context.RejectPrincipal();
    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
  }
}

