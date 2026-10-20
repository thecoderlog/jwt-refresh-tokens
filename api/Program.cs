using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = TokenService.SigningKey(builder.Configuration),
        ClockSkew = TimeSpan.Zero,   // the default is 5 minutes, which would let a 60 s token live 6
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

const string RefreshCookie = "refresh_token";

CookieOptions RefreshCookieOptions() => new()
{
    HttpOnly = true,                  // JavaScript can't read it
    Secure = true,                    // HTTPS only (localhost counts)
    SameSite = SameSiteMode.Strict,   // never sent from another site
    Path = "/auth",                   // only sent to the front desk
    MaxAge = TokenService.RefreshLifetime,
};

object IssuePair(HttpContext http, TokenService tokens, string userId, Guid familyId)
{
    http.Response.Cookies.Append(RefreshCookie, tokens.CreateRefreshToken(userId, familyId), RefreshCookieOptions());
    return new { accessToken = tokens.CreateAccessToken(userId), expiresIn = (int)TokenService.AccessLifetime.TotalSeconds };
}

app.MapPost("/auth/login", (LoginRequest body, HttpContext http, TokenService tokens) =>
    body is { Username: "dev", Password: "pass" }
        ? Results.Ok(IssuePair(http, tokens, body.Username, Guid.NewGuid()))
        : Results.Unauthorized());

app.MapPost("/auth/refresh", (HttpContext http, TokenService tokens) =>
{
    if (!http.Request.Cookies.TryGetValue(RefreshCookie, out var token) || tokens.Redeem(token) is not { } owner)
    {
        http.Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
        return Results.Unauthorized();
    }
    return Results.Ok(IssuePair(http, tokens, owner.UserId, owner.FamilyId));
});

app.MapPost("/auth/logout", (HttpContext http, TokenService tokens) =>
{
    if (http.Request.Cookies.TryGetValue(RefreshCookie, out var token)) tokens.Revoke(token);
    http.Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
    return Results.NoContent();
});

app.MapGet("/api/orders", (ClaimsPrincipal user) => new
{
    user = user.FindFirstValue(ClaimTypes.NameIdentifier),
    orders = new[] { new { id = 1042, item = "Keyboard", status = "Shipped" }, new { id = 1043, item = "Monitor", status = "Packing" } },
}).RequireAuthorization();

// Demo only: the register, so the video can show the rows. Never ship this.
if (app.Environment.IsDevelopment())
    app.MapGet("/debug/register", (TokenService tokens) => tokens.Rows());

app.Run();

record LoginRequest(string Username, string Password);
