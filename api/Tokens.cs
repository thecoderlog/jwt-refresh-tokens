using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

// One row per refresh token. The front desk's register.
public class RefreshToken
{
    public required string Hash { get; init; }      // SHA-256 of the token; the token itself is never stored
    public required string UserId { get; init; }
    public required Guid FamilyId { get; init; }    // every token from one login shares a family
    public required DateTime ExpiresAt { get; init; }
    public DateTime? UsedAt { get; set; }           // set when it's swapped for a new pair (rotation)
    public DateTime? RevokedAt { get; set; }        // set on logout, or when a used token comes back
}

public class TokenService(IConfiguration config)
{
    public static readonly TimeSpan AccessLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);

    readonly List<RefreshToken> register = [];

    public static SymmetricSecurityKey SigningKey(IConfiguration config) =>
        new(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));

    public string CreateAccessToken(string userId)
    {
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId)],
            expires: DateTime.UtcNow.Add(AccessLifetime),
            signingCredentials: new SigningCredentials(SigningKey(config), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken(string userId, Guid familyId)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        lock (register)
            register.Add(new RefreshToken
            {
                Hash = Hash(token),
                UserId = userId,
                FamilyId = familyId,
                ExpiresAt = DateTime.UtcNow.Add(RefreshLifetime),
            });
        return token;
    }

    // Swap a refresh token for a new pair. Returns null when the desk refuses it.
    public (string UserId, Guid FamilyId)? Redeem(string token)
    {
        lock (register)
        {
            var row = register.FirstOrDefault(r => r.Hash == Hash(token));
            if (row is null || row.RevokedAt is not null || row.ExpiresAt < DateTime.UtcNow)
                return null;

            if (row.UsedAt is not null)
            {
                // A token that was already swapped came back: someone has a copy. Revoke the whole family.
                RevokeFamily(row.FamilyId);
                return null;
            }

            row.UsedAt = DateTime.UtcNow;
            return (row.UserId, row.FamilyId);
        }
    }

    public void Revoke(string token)
    {
        lock (register)
        {
            var row = register.FirstOrDefault(r => r.Hash == Hash(token));
            if (row is not null) RevokeFamily(row.FamilyId);
        }
    }

    void RevokeFamily(Guid familyId)
    {
        foreach (var r in register.Where(r => r.FamilyId == familyId && r.RevokedAt is null))
            r.RevokedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<RefreshToken> Rows() { lock (register) return [.. register]; }

    static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
