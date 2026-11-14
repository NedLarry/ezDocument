using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleApi.Models;

namespace SampleApi.Controllers;

/// <summary>Create, read and manage customer orders.</summary>
[ApiController]
[Authorize]
[Route(Routes.Orders)]
public class OrdersController : ControllerBase
{
    /// <summary>Lists orders, optionally filtered by status.</summary>
    /// <param name="status">Only return orders in this status.</param>
    /// <param name="page">1-based page number.</param>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<OrderDto>> List([FromQuery] OrderStatus? status, int page = 1, CancellationToken cancellationToken = default)
        => Ok(Array.Empty<OrderDto>());

    /// <summary>Gets a single order.</summary>
    /// <param name="id">Order identifier.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(200)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<OrderDto> Get(Guid id) => NotFound();

    /// <summary>Places a new order.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        await Task.CompletedTask;
        return Created("", new OrderDto(Guid.NewGuid(), OrderStatus.Pending, 0));
    }

    /// <summary>Changes the status of an order.</summary>
    [HttpPatch("{id:guid}/status")]
    public IActionResult UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest body) => NoContent();

    [HttpDelete("{id:guid}")]
    public IActionResult Cancel(Guid id, [FromServices] ILogger<OrdersController> logger) => NoContent();

    /// <summary>Uploads an invoice PDF for the order.</summary>
    [HttpPost("{id:guid}/invoice")]
    public IActionResult UploadInvoice(Guid id, IFormFile file, [FromForm] string? comment) => Ok();

    [NonAction]
    public void Helper() { }
}
