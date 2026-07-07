using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Controllers;

public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try { return Ok(await _auth.RegisterAsync(request)); }
        catch (AuthException ex) { return BadRequest(new ErrorResponse { Error = ex.Message }); }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        try { return Ok(await _auth.LoginAsync(request)); }
        catch (AuthException ex) { return Unauthorized(new ErrorResponse { Error = ex.Message }); }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        try { return Ok(await _auth.RefreshAsync(request.RefreshToken)); }
        catch (AuthException ex) { return Unauthorized(new ErrorResponse { Error = ex.Message }); }
    }

    /// <summary>Start password recovery. Always returns 200 so it never reveals whether the email exists.</summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _auth.ForgotPasswordAsync(request);
        return Ok();
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword(ResetPasswordWithTokenRequest request)
    {
        try { await _auth.ResetPasswordAsync(request); return Ok(); }
        catch (AuthException ex) { return BadRequest(new ErrorResponse { Error = ex.Message }); }
    }

    /// <summary>The signed-in user's own profile.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me() => Ok(await _auth.GetMeAsync(CurrentUserId));

    /// <summary>Update the signed-in user's name/phone; returns a fresh token pair with updated claims.</summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<AuthResponse>> UpdateProfile(UpdateProfileRequest request)
    {
        try { return Ok(await _auth.UpdateProfileAsync(CurrentUserId, request)); }
        catch (AuthException ex) { return BadRequest(new ErrorResponse { Error = ex.Message }); }
    }

    /// <summary>Change the signed-in user's password; returns a fresh token pair.</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        try { return Ok(await _auth.ChangePasswordAsync(CurrentUserId, request)); }
        catch (AuthException ex) { return BadRequest(new ErrorResponse { Error = ex.Message }); }
    }
}
