using DarkVelocity.Auth.Api.Dtos;
using DarkVelocity.Auth.Api.Entities;

namespace DarkVelocity.Auth.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginWithPinAsync(string pin, Guid? locationId);
    Task<LoginResponse?> LoginWithQrAsync(string token, Guid? locationId);
    Task<LoginResponse?> RefreshTokenAsync(string refreshToken);
    string HashPin(string pin);
    bool VerifyPin(string pin, string hash);
}
