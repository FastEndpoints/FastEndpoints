using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace FastEndpoints.Security
{
    public static class AuthExtensions
    {
        /// <summary>
        /// configure and enable jwt bearer authentication
        /// </summary>
        /// <param name="tokenSigningKey">the secret key to use for verifying the jwt tokens</param>
        public static IServiceCollection AddAuthenticationJWTBearer(this IServiceCollection services, string tokenSigningKey)
        {
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(tokenSigningKey))
                };
            });

            return services;
        }
    }
}
