﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HackMyHabit.Domain.Users.Commons
{
    public interface IAuthManager
    {
        JsonWebToken CreateToken(Guid userId, string? role = null, string? audience = null, IDictionary<string, IEnumerable<string>>? claims = null);
    }

    public sealed class AuthManager : IAuthManager
    {
        private static readonly Dictionary<string, IEnumerable<string>> EmptyClaims = new();
        private readonly AuthOptions options;
        private readonly SigningCredentials signingCredentials;
        private readonly string issuer;

        public AuthManager(IOptions<AuthOptions> options)
        {
            this.options = options.Value;

            var issuerSigningKey = this.options.IssuerSigningKey;
            if (issuerSigningKey is null)
            {
                throw new InvalidOperationException("Issuer signing key not set.");
            }

            this.signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.IssuerSigningKey)), SecurityAlgorithms.HmacSha256);
            this.issuer = this.options.Issuer;
        }

        public JsonWebToken CreateToken(Guid userId, string? role = null, string? audience = null,
            IDictionary<string, IEnumerable<string>>? claims = null)
        {
            var now = DateTime.UtcNow;
            var jwtClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeMilliseconds().ToString())
        };
            if (!string.IsNullOrWhiteSpace(role))
            {
                jwtClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (!string.IsNullOrWhiteSpace(audience))
            {
                jwtClaims.Add(new Claim(JwtRegisteredClaimNames.Aud, audience));
            }

            if (claims?.Any() is true)
            {
                var customClaims = new List<Claim>();
                foreach (var (claim, values) in claims)
                {
                    customClaims.AddRange(values.Select(value => new Claim(claim, value)));
                }

                jwtClaims.AddRange(customClaims);
            }

            var expires = now.Add(options.Expiry);

            var jwt = new JwtSecurityToken(
                issuer,
                claims: jwtClaims,
                notBefore: now,
                expires: expires,
                signingCredentials: signingCredentials
            );

            var token = new JwtSecurityTokenHandler().WriteToken(jwt);

            return new JsonWebToken
            {
                AccessToken = token,
                Expiry = new DateTimeOffset(expires).ToUnixTimeMilliseconds(),
                UserId = userId,
                Role = role ?? string.Empty,
                Claims = claims ?? EmptyClaims
            };
        }
    }
}
