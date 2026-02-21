using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scoreboard.Api.Services;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
	private readonly AuthService _authService;
	private readonly ILogger<AuthController> _logger;

	public AuthController(AuthService authService, ILogger<AuthController> logger)
	{
		_authService = authService;
		_logger = logger;
	}

	[AllowAnonymous]
	[HttpPost("signup")]
	public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
	{
		if (!ModelState.IsValid) return BadRequest(ModelState);

		try
		{
			var result = await _authService.SignUpAsync(request);
			if (!result.Success) return BadRequest(result);
			return Ok(result);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Signup failed for {Email}", request.EmailAddress);
			return StatusCode(500, new AuthResponse
			{
				Success = false,
				Message = "An error occurred during signup. Please try again or contact support if the issue persists."
			});
		}
	}

	[AllowAnonymous]
	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginRequest request)
	{
		if (!ModelState.IsValid) return BadRequest(ModelState);

		var result = await _authService.LoginAsync(request);
		if (!result.Success) return Unauthorized(result);
		return Ok(result);
	}

	[AllowAnonymous]
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
			_logger.LogError(ex, "Coupon validation failed");
			return StatusCode(500, new
			{
				error = "An error occurred while validating the coupon.",
				message = "Please try again or contact support if the issue persists."
			});
		}
	}
}
