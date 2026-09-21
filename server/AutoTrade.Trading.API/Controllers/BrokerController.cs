using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Broker;
using AutoTrade.Trading.Core.Configuration;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Broker;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.API.Controllers
{
  [ApiController]
  [Route("api/broker")]
  public class BrokerController(IHikyaku hikyaku, IConfiguration configuration) : ControllerBase
  {
    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(BrokerConnectionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
      BrokerConnectionStatusDto status = await hikyaku.Send(new GetBrokerConnectionStatus(), cancellationToken);

      return Ok(status);
    }

    [HttpGet("snapshot")]
    [Authorize]
    [ProducesResponseType(typeof(BrokerSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
      BrokerSnapshotDto snapshot = await hikyaku.Send(new GetBrokerSnapshot(), cancellationToken);

      return Ok(snapshot);
    }

    [HttpPost("authorization")]
    [Authorize]
    
    [ProducesResponseType(typeof(BrokerAuthorizationStartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartAuthorization(CancellationToken cancellationToken)
    {
      BrokerAuthorizationStartDto result = await hikyaku.Send(new StartBrokerAuthorization
      {
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BrokerAuthorizationOutcome.Applied)
      {
        return Conflict();
      }

      return Ok(result);
    }

    /// <summary>
    /// Provider callback. It is anonymous on purpose: the provider redirects the browser here, which is a
    /// cross-site navigation, so the session cookie is not guaranteed to be sent. The request is bound to
    /// the operator by the single-use correlator issued when the flow started, and the browser is sent back
    /// to the client application with a coarse result flag so no provider detail leaks into the URL.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteAuthorization([FromQuery] string code, [FromQuery] string state, CancellationToken cancellationToken)
    {
      BrokerAuthorizationResultDto result = await hikyaku.Send(new CompleteBrokerAuthorization
      {
        Code = code,
        CorrelationId = state
      }, cancellationToken);

      string returnUri = configuration[BrokerConfigurationKeys.ReturnUri];

      if (string.IsNullOrWhiteSpace(returnUri))
      {
        return Ok(new
        {
          outcome = result.Outcome.ToString()
        });
      }

      string separator = returnUri.Contains("?", StringComparison.Ordinal) ? "&" : "?";

      return Redirect(returnUri + separator + "broker=" + ToReturnFlag(result.Outcome));
    }

    [HttpPost("authorization/revoke")]
    [Authorize]
    
    [ProducesResponseType(typeof(BrokerAuthorizationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAuthorization(CancellationToken cancellationToken)
    {
      BrokerAuthorizationResultDto result = await hikyaku.Send(new RevokeBrokerAuthorization
      {
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome == BrokerAuthorizationOutcome.NotFound)
      {
        return NotFound();
      }

      return Ok(result);
    }

    /// <summary>
    /// Imports a token pair issued outside the consent flow, which is how the official Playground provides
    /// credentials while an application awaits approval. The endpoint reports not found when the
    /// affordance is disabled, so a disabled deployment does not advertise it.
    /// </summary>
    [HttpPost("tokens")]
    [Authorize]
    
    [ProducesResponseType(typeof(BrokerAuthorizationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportTokens([FromBody] BrokerTokenImportDto request, CancellationToken cancellationToken)
    {
      BrokerAuthorizationResultDto result = await hikyaku.Send(new ImportBrokerTokens
      {
        AccessToken = request.AccessToken,
        RefreshToken = request.RefreshToken,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome == BrokerAuthorizationOutcome.Applied)
      {
        return Ok(result);
      }

      if (result.Outcome == BrokerAuthorizationOutcome.NotConfigured)
      {
        return NotFound();
      }

      return BadRequest(result);
    }

    /// <summary>
    /// Opens a real connection to the broker endpoint and reports how far the handshake gets. It uses the
    /// application credentials plus, when a grant exists, the stored account authorization; it never
    /// returns a credential and never writes state.
    /// </summary>
    [HttpPost("probe")]
    [Authorize]
    
    [ProducesResponseType(typeof(BrokerProbeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProbeConnection(CancellationToken cancellationToken)
    {
      BrokerProbeResultDto result = await hikyaku.Send(new ProbeBrokerConnection
      {
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      // Diagnostics disabled: the endpoint does not exist as far as the outside world is concerned.
      if (!result.IsAuthenticated && string.IsNullOrEmpty(result.Description))
      {
        return NotFound();
      }

      return Ok(result);
    }

    private static string ToReturnFlag(BrokerAuthorizationOutcome outcome)
    {
      switch (outcome)
      {
        case BrokerAuthorizationOutcome.Applied:
          return "authorized";

        case BrokerAuthorizationOutcome.ProviderRejected:
          return "provider-rejected";

        case BrokerAuthorizationOutcome.InvalidCorrelation:
          return "expired";

        default:
          return "invalid";
      }
    }

    private Guid ReadOperatorId()
    {
      Claim claim = User.FindFirst(ClaimTypes.NameIdentifier);

      return claim != null && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : Guid.Empty;
    }
  }
}
