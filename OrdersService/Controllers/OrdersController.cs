using Microsoft.AspNetCore.Mvc;
using OrdersService.Models;
using OrdersService.Services;
using System.ComponentModel.DataAnnotations;

namespace OrdersService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request, [FromHeader(Name = "userId")] string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID is required in header");

        try
        {
            var order = await _orderService.CreateOrderAsync(userId, request.Amount, request.Description);
            _logger.LogInformation("Order created: {OrderId} for user {UserId} with amount {Amount}", 
                order.Id, userId, request.Amount);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Order creation failed for user {UserId}: {Error}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromHeader(Name = "userId")] string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID is required in header");

        try
        {
            var orders = await _orderService.GetOrdersByUserIdAsync(userId);
            return Ok(orders);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Failed to get orders for user {UserId}: {Error}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }
    
    [HttpGet("test")]
    public async Task<ActionResult<object>> TestEndpoint()
    {
        return Ok(new { 
            message = "Orders service is working", 
            timestamp = DateTime.UtcNow,
            status = "healthy"
        });
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByUserId(string userId)
    {
        var orders = await _orderService.GetOrdersByUserIdAsync(userId);
        return Ok(orders);
    }
}

public class CreateOrderRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive")]
    public decimal Amount { get; set; }
    
    [Required]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;
}
