using Microsoft.AspNetCore.Mvc;
using Scoreboard.Api.Services;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        var result = await _authService.SignUpAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success) return Unauthorized(result);
        return Ok(result);
    }

    [HttpGet("validate-coupon/{code}")]
    public async Task<IActionResult> ValidateCoupon(string code)
    {
        try
        {
            var result = await _authService.ValidateCouponAsync(code);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                Error = "An error occurred while validating the coupon.",
                Message = "Please contact customer support if the error persists.",
                Details = ex.Message,
                InnerException = ex.InnerException?.Message
            });
        }
    }
}
