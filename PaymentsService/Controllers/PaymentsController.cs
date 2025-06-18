using Microsoft.AspNetCore.Mvc;
using PaymentsService.Models;
using PaymentsService.Services;
using System.ComponentModel.DataAnnotations;

namespace PaymentsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("create-account")]
    public async Task<ActionResult<Account>> CreateAccount([FromBody] CreateAccountRequest request, [FromHeader(Name = "userId")] string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID is required in header");

        try
        {
            var account = await _paymentService.CreateAccountAsync(userId, request.InitialBalance);
            _logger.LogInformation("Account created for user {UserId} with initial balance {Balance}", userId, request.InitialBalance);
            return CreatedAtAction(nameof(GetAccount), new { userId = account.UserId }, account);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Account creation failed for user {UserId}: {Error}", userId, ex.Message);
            return Conflict(ex.Message);
        }
    }

    [HttpPost("add-funds")]
    public async Task<IActionResult> AddFunds([FromBody] AddFundsRequest request, [FromHeader(Name = "userId")] string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID is required in header");

        try
        {
            await _paymentService.DepositAsync(userId, request.Amount);
            _logger.LogInformation("Funds added for user {UserId}: {Amount}", userId, request.Amount);
            
            return Ok(new { 
                Message = "Funds added successfully",
                UserId = userId,
                Amount = request.Amount,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Fund addition failed for user {UserId}: {Error}", userId, ex.Message);
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid fund addition request for user {UserId}: {Error}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("get-account")]
    public async Task<ActionResult<Account>> GetAccount([FromHeader(Name = "userId")] string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID is required in header");

        var account = await _paymentService.GetAccountByUserIdAsync(userId);
        if (account == null)
            return NotFound("Account not found");

        return Ok(account);
    }

    [HttpGet("{userId}/balance")]
    public async Task<ActionResult<object>> GetBalance(string userId)
    {
        var balance = await _paymentService.GetBalanceAsync(userId);
        if (balance == null)
            return NotFound("Account not found");

        return Ok(new { 
            UserId = userId,
            Balance = balance.Value,
            Timestamp = DateTime.UtcNow
        });
    }
    
    [HttpGet("test")]
    public async Task<ActionResult<object>> TestEndpoint()
    {
        return Ok(new { 
            message = "Payments service is working", 
            timestamp = DateTime.UtcNow,
            status = "healthy"
        });
    }
}

public class CreateAccountRequest
{
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Initial balance must be non-negative")]
    public decimal InitialBalance { get; set; }
}

public class AddFundsRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive")]
    public decimal Amount { get; set; }
}
