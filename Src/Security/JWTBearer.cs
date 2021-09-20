using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EZEndpoints.Security
{
    public static class JWTBearer
    {
        public static string CreateToken(
            string signingKey,
            DateTime? expireAt,
            IEnumerable<string>? permissions,
            IEnumerable<string>? roles,
            IEnumerable<System.Security.Claims.Claim>? claims)
        {
            var claimList = new List<System.Security.Claims.Claim>();

            if (claims != null)
                claimList.AddRange(claims);

            if (permissions != null)
                claimList.Add(new System.Security.Claims.Claim(Claim.Permissions, string.Join(',', permissions)));

            if (roles != null)
                claimList.AddRange(roles.Select(r => new System.Security.Claims.Claim(ClaimTypes.Role, r)));

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
