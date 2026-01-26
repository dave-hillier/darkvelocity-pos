using DarkVelocity.Auth.Api.Dtos;
using DarkVelocity.Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginWithPinAsync(request.Pin, request.LocationId);

        if (result == null)
            return Unauthorized(new { message = "Invalid PIN" });

        return Ok(result);
    }

    [HttpPost("login/qr")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginWithQr([FromBody] QrLoginRequest request)
    {
        var result = await _authService.LoginWithQrAsync(request.Token, request.LocationId);

        if (result == null)
            return Unauthorized(new { message = "Invalid QR token" });

        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (result == null)
            return Unauthorized(new { message = "Invalid refresh token" });

        return Ok(result);
    }
}
