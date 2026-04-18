using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.WebApi.Common;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController : BaseController
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;

    public SalesController(IMediator mediator, IMapper mapper)
    {
        _mediator = mediator;
        _mapper = mapper;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseWithData<CreateSaleResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var command = _mapper.Map<CreateSaleCommand>(request);
        var result = await _mediator.Send(command, ct);

        return Created(string.Empty, new ApiResponseWithData<CreateSaleResult>
        {
            Success = true,
            Message = "Sale created successfully",
            Data = result
        });
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseWithData<ListSalesResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSales(
        [FromQuery] int _page = 1,
        [FromQuery] int _size = 10,
        [FromQuery] string? _order = null,
        CancellationToken ct = default)
    {
        var query = new ListSalesQuery { Page = _page, Size = _size, Order = _order };
        var result = await _mediator.Send(query, ct);
        return Ok(new ApiResponseWithData<ListSalesResult> { Success = true, Data = result });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<GetSaleResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSale([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSaleQuery { Id = id }, ct);
        return Ok(new ApiResponseWithData<GetSaleResult> { Success = true, Data = result });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<UpdateSaleResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSale([FromRoute] Guid id, [FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var command = _mapper.Map<UpdateSaleCommand>(request);
        command.Id = id;
        var result = await _mediator.Send(command, ct);
        return Ok(new ApiResponseWithData<UpdateSaleResult> { Success = true, Data = result });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSale([FromRoute] Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteSaleCommand { Id = id }, ct);
        return Ok(new ApiResponse { Success = true, Message = "Sale cancelled successfully" });
    }

    [HttpPatch("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponseWithData<CancelSaleResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSale([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelSaleCommand { Id = id }, ct);
        return Ok(new ApiResponseWithData<CancelSaleResult> { Success = true, Data = result });
    }

    [HttpPatch("{id:guid}/items/{itemId:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponseWithData<CancelSaleItemResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSaleItem([FromRoute] Guid id, [FromRoute] Guid itemId, CancellationToken ct)
    {
        var command = new CancelSaleItemCommand { SaleId = id, ItemId = itemId };
        var result = await _mediator.Send(command, ct);
        return Ok(new ApiResponseWithData<CancelSaleItemResult> { Success = true, Data = result });
    }
}
