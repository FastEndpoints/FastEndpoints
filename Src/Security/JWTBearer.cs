using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ApiExpress.Security
{
    public static class JWTBearer
    {
        public static string CreateTokenWithClaims(
            string signingKey,
            DateTime? expireAt = null,
            IEnumerable<string>? permissions = null,
            IEnumerable<string>? roles = null,
            params (string claimType, string claimValue)[] claims)
                => CreateToken(
                    signingKey,
                    expireAt,
                    permissions,
                    roles,
                    claims.Select(c => new Claim(c.claimType, c.claimValue)));

        public static string CreateToken(
            string signingKey,
            DateTime? expireAt = null,
            IEnumerable<string>? permissions = null,
            IEnumerable<string>? roles = null,
            IEnumerable<Claim>? claims = null)
        {
            var claimList = new List<Claim>();

            if (claims != null)
                claimList.AddRange(claims);

            if (permissions != null)
                claimList.Add(new Claim(ClaimTypes.Permissions, string.Join(',', permissions)));

            if (roles != null)
                claimList.AddRange(roles.Select(r => new Claim(System.Security.Claims.ClaimTypes.Role, r)));

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claimList),
                Expires = expireAt,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(signingKey)),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }
    }
}
