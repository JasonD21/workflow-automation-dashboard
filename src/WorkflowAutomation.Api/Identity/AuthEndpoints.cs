using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace WorkflowAutomation.Api.Identity;

public record RegisterRequest(string Email, string Password, string? DisplayName);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
public record MeResponse(Guid Id, string Email, string? DisplayName, string TimeZone);

public static class AuthEndpoints
{
    private const string RefreshCookie = "refresh_token";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout);
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapPost("/demo-login", DemoLogin);

        return app;
    }

    private static async Task<IResult> Register(RegisterRequest req, UserManager<ApplicationUser> users, ITokenService tokens,
        IOptions<JwtOptions> jwt, HttpResponse res, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            DisplayName = req.DisplayName
        };

        var result = await users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return Results.ValidationProblem(
                result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        return await IssueTokens(user, tokens, jwt, res, ct);
    }

    private static async Task<IResult> Login(LoginRequest req, UserManager<ApplicationUser> users, ITokenService tokens,
        IOptions<JwtOptions> jwt, HttpResponse res, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null || !await users.CheckPasswordAsync(user, req.Password))
            return Results.Unauthorized();

        return await IssueTokens(user, tokens, jwt, res, ct);
    }

    private static async Task<IResult> Refresh(UserManager<ApplicationUser> users, ITokenService tokens, IOptions<JwtOptions> jwt,
        HttpRequest req, HttpResponse res, CancellationToken ct)
    {
        var raw = req.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(raw)) return Results.Unauthorized();

        var rotated = await tokens.RotateRefreshTokenAsync(raw, ct);
        if (rotated is null)
        {
            ClearRefreshCookie(res);
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(rotated.Value.UserId.ToString());
        if (user is null)
        {
            ClearRefreshCookie(res);
            return Results.Unauthorized();
        }

        var access = tokens.CreateAccessToken(user);
        SetRefreshCookie(res, rotated.Value.RefreshToken, jwt.Value.RefreshTokenDays);
        return Results.Ok(new AuthResponse(access.Token, access.ExpiresAtUtc));
    }

    private static async Task<IResult> Logout(ITokenService tokens, HttpRequest req, HttpResponse res, CancellationToken ct)
    {
        var raw = req.Cookies[RefreshCookie];
        if (!string.IsNullOrEmpty(raw))
            await tokens.RevokeRefreshTokenAsync(raw, ct);

        ClearRefreshCookie(res);
        return Results.NoContent();
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(principal.GetUserId().ToString());
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new MeResponse(user.Id, user.Email!, user.DisplayName, user.TimeZone));
    }

    private static async Task<IResult> DemoLogin(UserManager<ApplicationUser> users, ITokenService tokens, IOptions<JwtOptions> jwt,
    IConfiguration config, HttpResponse res, CancellationToken ct)
    {
        var demoEmail = config["Demo:Email"];
        var user = demoEmail is null ? null : await users.FindByEmailAsync(demoEmail);
        if (user is null)
            return Results.Problem("Demo account is not available.", statusCode: StatusCodes.Status503ServiceUnavailable);

        return await IssueTokens(user, tokens, jwt, res, ct);
    }

    private static async Task<IResult> IssueTokens(ApplicationUser user, ITokenService tokens, IOptions<JwtOptions> jwt,
        HttpResponse res, CancellationToken ct)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = await tokens.CreateRefreshTokenAsync(user.Id, ct);
        SetRefreshCookie(res, refresh, jwt.Value.RefreshTokenDays);
        return Results.Ok(new AuthResponse(access.Token, access.ExpiresAtUtc));
    }

    private static void SetRefreshCookie(HttpResponse res, string token, int days) =>
        res.Cookies.Append(RefreshCookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,   // cross-site SPA → API
            Path = "/api/auth",             // only sent to refresh/logout
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            IsEssential = true
        });

    private static void ClearRefreshCookie(HttpResponse res) =>
        res.Cookies.Delete(RefreshCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/api/auth"
        });
}