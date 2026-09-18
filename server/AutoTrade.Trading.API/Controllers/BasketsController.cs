using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Basket;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Basket;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTrade.Trading.API.Controllers
{
  [ApiController]
  [Route("api/baskets")]
  [Authorize]
  public class BasketsController(IHikyaku hikyaku) : ControllerBase
  {
    [HttpGet]
    [ProducesResponseType(typeof(List<BasketSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBaskets(CancellationToken cancellationToken)
    {
      List<BasketSummaryDto> baskets = await hikyaku.Send(new GetBaskets(), cancellationToken);

      return Ok(baskets);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveBasket(CancellationToken cancellationToken)
    {
      BasketDetailDto basket = await hikyaku.Send(new GetActiveBasket(), cancellationToken);

      return basket == null ? NotFound() : Ok(basket);
    }

    [HttpGet("{basketId:guid}")]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBasketById(Guid basketId, CancellationToken cancellationToken)
    {
      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = basketId
      }, cancellationToken);

      return basket == null ? NotFound() : Ok(basket);
    }

    [HttpGet("{basketId:guid}/versions")]
    [ProducesResponseType(typeof(List<BasketVersionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBasketVersions(Guid basketId, CancellationToken cancellationToken)
    {
      List<BasketVersionDto> versions = await hikyaku.Send(new GetBasketVersionsByBasket
      {
        BasketId = basketId
      }, cancellationToken);

      return Ok(versions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBasket([FromBody] BasketIdentityDto request, CancellationToken cancellationToken)
    {
      CreateBasketResult result = await hikyaku.Send(new CreateBasket
      {
        Name = request.Name,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = result.BasketId
      }, cancellationToken);

      return Ok(basket);
    }

    [HttpPost("{basketId:guid}/clone")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloneBasket(Guid basketId, [FromBody] BasketIdentityDto request, CancellationToken cancellationToken)
    {
      CreateBasketResult result = await hikyaku.Send(new CloneBasket
      {
        SourceBasketId = basketId,
        Name = request.Name,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = result.BasketId
      }, cancellationToken);

      return Ok(basket);
    }

    [HttpPatch("{basketId:guid}/identity")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBasketIdentity(Guid basketId, [FromBody] BasketIdentityDto request, CancellationToken cancellationToken)
    {
      BasketOperationResult result = await hikyaku.Send(new UpdateBasketIdentity
      {
        BasketId = basketId,
        Name = request.Name,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = basketId
      }, cancellationToken);

      return Ok(basket);
    }

    [HttpPatch("{basketId:guid}/composition")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBasketComposition(Guid basketId, [FromBody] List<BasketCompositionLegDto> legs, CancellationToken cancellationToken)
    {
      BasketOperationResult result = await hikyaku.Send(new UpdateBasketComposition
      {
        BasketId = basketId,
        Legs = legs,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = basketId
      }, cancellationToken);

      return Ok(basket);
    }

    [HttpPatch("{basketId:guid}/policy")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBasketPolicy(Guid basketId, [FromBody] BasketPolicyDto policy, CancellationToken cancellationToken)
    {
      BasketOperationResult result = await hikyaku.Send(new UpdateBasketPolicy
      {
        BasketId = basketId,
        Policy = policy,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = basketId
      }, cancellationToken);

      return Ok(basket);
    }

    [HttpPost("{basketId:guid}/versions")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishBasketVersion(Guid basketId, [FromBody] BasketVersionPublicationDto request, CancellationToken cancellationToken)
    {
      PublishBasketVersionResult result = await hikyaku.Send(new PublishBasketVersion
      {
        BasketId = basketId,
        Note = request.Note,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = basketId
      }, cancellationToken);

      return Ok(basket);
    }

    [HttpPost("{basketId:guid}/versions/{versionId:guid}/activate")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActivateBasketVersion(Guid basketId, Guid versionId, CancellationToken cancellationToken)
    {
      BasketOperationResult result = await hikyaku.Send(new ActivateBasketVersion
      {
        BasketId = basketId,
        VersionId = versionId,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = basketId
      }, cancellationToken);

      return Ok(basket);
    }

    [HttpPost("{basketId:guid}/archive")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(BasketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveBasket(Guid basketId, CancellationToken cancellationToken)
    {
      BasketOperationResult result = await hikyaku.Send(new ArchiveBasket
      {
        BasketId = basketId,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome != BasketOperationOutcome.Applied)
      {
        return MapOutcome(result.Outcome);
      }

      BasketDetailDto basket = await hikyaku.Send(new GetBasketById
      {
        BasketId = basketId
      }, cancellationToken);

      return Ok(basket);
    }

    private IActionResult MapOutcome(BasketOperationOutcome outcome)
    {
      switch (outcome)
      {
        case BasketOperationOutcome.InvalidInput:
          return BadRequest();

        case BasketOperationOutcome.Conflict:
        case BasketOperationOutcome.InvalidState:
          return Conflict();

        case BasketOperationOutcome.NotFound:
          return NotFound();

        default:
          return BadRequest();
      }
    }

    private Guid ReadOperatorId()
    {
      Claim claim = User.FindFirst(ClaimTypes.NameIdentifier);

      return claim != null && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : Guid.Empty;
    }
  }
}
