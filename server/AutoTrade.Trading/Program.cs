using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoTrade.Trading;
using AutoTrade.Trading.API;
using AutoTrade.Trading.API.Controllers;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Session;
using AutoTrade.Trading.Handlers;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Analysis;
using AutoTrade.Trading.Handlers.Evidence;
using AutoTrade.Trading.Handlers.Execution;
using AutoTrade.Trading.Handlers.MarketData;
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

// Optional untracked override for local secrets; it is listed in .gitignore and wins over the
// environment files because it is added last. The configuration keys never change.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

IConfiguration configuration = builder.Configuration;

// Exactly one mediator for the whole module.
builder.Services.AddHikyaku(hikyaku => hikyaku.RegisterServicesFromAssembly(typeof(AutoTrade.Trading.Handlers.Module).Assembly));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTradingApi();
builder.Services.AddTradingHandlers(configuration);

// The analysis cycle is host infrastructure: it owns the wake up interval and dispatches the cycle command,
// which owns the rules. Without configured timing it stops instead of choosing a pace of its own.
builder.Services.AddHostedService<AnalysisCycleService>();
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

// Fail-closed: a broker that is enabled must be fully configured, and a broker that is not configured
// is a supported state that leaves the rest of the application working.
BrokerOptions brokerOptions = app.Services.GetRequiredService<BrokerOptions>();
BrokerConfigurationGuard.EnsureValid(brokerOptions);

// Fail-closed: the market data source must be usable. None means no data, which the gates report as
// blocking; selecting a source that cannot work aborts startup instead of pretending a feed exists.
MarketDataOptions marketDataOptions = app.Services.GetRequiredService<MarketDataOptions>();
MarketDataModule.EnsureSourceIsUsable(marketDataOptions, brokerOptions);

// Fail-closed: an execution provider that cannot work aborts startup, and no provider at all is a supported
// state in which nothing can be sent.
ExecutionOptions executionOptions = app.Services.GetRequiredService<ExecutionOptions>();
ExecutionModule.EnsureProviderIsUsable(executionOptions);

// Fail-closed: asking for a semantic memory without saying where it lives aborts startup, because coming up
// without the capability that was configured would be worse than refusing. No provider is a supported state.
EvidenceOptions evidenceOptions = app.Services.GetRequiredService<EvidenceOptions>();
EvidenceModule.EnsureProviderIsUsable(evidenceOptions);

// The store is opened now, not on first use: a configured store that cannot be opened has to stop the host
// rather than surface as a failed retrieval hours later.
app.Services.GetRequiredService<IJigenEvidenceStore>();

// Fail-closed: asking for embeddings without naming the checkpoint aborts startup, and the source is built now
// so a missing or unreadable checkpoint fails here instead of inside a retrieval. No provider is a supported
// state in which the semantic memory simply cannot be written to.
EmbeddingOptions embeddingOptions = app.Services.GetRequiredService<EmbeddingOptions>();
EmbeddingModule.EnsureProviderIsUsable(embeddingOptions);
app.Services.GetRequiredService<ITextEmbeddingSource>();

// Fail-closed: asking for an analysis model without naming it aborts startup, and a model the local engine
// does not have aborts startup too. A model that is quietly missing would turn every opinion into a silent
// absence, and absence must never be readable as a neutral view. No provider is a supported state.
AnalysisOptions analysisOptions = app.Services.GetRequiredService<AnalysisOptions>();
AnalysisModule.EnsureProviderIsUsable(analysisOptions);
await app.Services.GetRequiredService<IOllamaAnalysisClient>().EnsureModelIsPresentAsync(CancellationToken.None);

app.Logger.LogInformation("Market data source in force: {Provider}.", marketDataOptions.Provider);
app.Logger.LogInformation("Execution provider in force: {Provider}.", executionOptions.Provider);
app.Logger.LogInformation("Evidence store in force: {Provider}.", evidenceOptions.Provider);
app.Logger.LogInformation("Embedding source in force: {Provider}/{Model}.", embeddingOptions.Engine, embeddingOptions.Engine == AutoTrade.Trading.Handlers.Evidence.EmbeddingEngineKind.Unset ? "none" : embeddingOptions.ModelName);
app.Logger.LogInformation("Analysis model in force: {Provider}/{Model}.", analysisOptions.Provider, string.IsNullOrWhiteSpace(analysisOptions.Model) ? "none" : analysisOptions.Model);

if (!brokerOptions.IsClientConfigured)
{
  app.Logger.LogWarning("Broker is not configured. Set Trading:Broker:ClientId, ClientSecret, TokenKey and RedirectUri to enable it.");
}

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

