using AnchorSafe.API.Helpers;
using log4net;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;

namespace AnchorSafe.API.Services
{
    /// <summary>
    /// Handles JWT token creation and validation.
    /// </summary>
    public class TokenService
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Extracts the token string from the Authorization header.
        /// </summary>
        public static string GetTokenFromHeader(AuthenticationHeaderValue auth)
        {
            log.Info("Entering GetTokenFromHeader()");
            string token = null;
            if (auth != null)
            {
                token = auth.Parameter;
                log.Debug($"Extracted token parameter '{token}' from header");
            }
            else
            {
                log.Warn("Authorization header is null");
            }
            log.Info($"Exiting GetTokenFromHeader() with result {(token != null ? "[REDACTED]" : "null")}");
            return token;
        }

        /// <summary>
        /// Generates a JWT for the specified username.
        /// </summary>
        public static string GenerateToken(String username, Int32 expiryDuration)
        {
            log.Info("Entering GenerateToken()");
            log.Debug($"Parameter username='{username}'");
            string jwt = null;
            try
            {
                string secret = ConfigurationHelper.Configuration["AS_API_TokenSecret"] ?? string.Empty;
                byte[] key = Convert.FromBase64String(secret);
                log.Debug("Decoded token secret key from configuration");

                SymmetricSecurityKey securityKey = new SymmetricSecurityKey(key);
                SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
                SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
                    Expires = DateTime.UtcNow.AddMinutes(expiryDuration),
                    SigningCredentials = credentials
                };
                log.Debug("Created SecurityTokenDescriptor");

                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                JwtSecurityToken token = handler.CreateJwtSecurityToken(descriptor);
                jwt = handler.WriteToken(token);
                log.Info("Generated JWT successfully");
            }
            catch (Exception ex)
            {
                log.Error("Error generating token", ex);
                throw;
            }

            log.Info("Exiting GenerateToken()");
            return jwt;
        }

        /// <summary>
        /// Validates and returns the principal from the JWT.
        /// </summary>
        public static ClaimsPrincipal GetPrincipal(string token)
        {
            log.Info("Entering GetPrincipal()");
            log.Debug(token != null ? "Token provided" : "Token is null");
            try
            {
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                JwtSecurityToken jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jwtToken == null)
                {
                    log.Warn("ReadToken did not return a JwtSecurityToken");
                    return null;
                }
                log.Debug("JWT parsed successfully");

                Boolean validateExpiration = !Api.IsApplicationDevMode();

                string secret = ConfigurationHelper.Configuration["AS_API_TokenSecret"] ?? string.Empty;
                byte[] key = Convert.FromBase64String(secret);
                TokenValidationParameters parameters = new TokenValidationParameters
                {
                    RequireExpirationTime = validateExpiration,
                    ValidateLifetime = validateExpiration,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };

                log.Debug("Validating token with TokenValidationParameters");
                SecurityToken validatedToken;
                ClaimsPrincipal principal = handler.ValidateToken(token, parameters, out validatedToken);
                log.Info("Token validated successfully");
                log.Info("Exiting GetPrincipal() with valid principal");
                return principal;
            }
            catch (Exception ex)
            {
                log.Error("Error validating token", ex);
                log.Info("Exiting GetPrincipal() with null");
                return null;
            }
        }

        /// <summary>
        /// Validates the JWT and returns the username claim if valid.
        /// </summary>
        public static String ValidateToken(String token)
        {
            log.Info("Entering ValidateToken()");
            string username = null;
            if (string.IsNullOrEmpty(token))
            {
                log.Warn("ValidateToken called with null or empty token");
            }
            else
            {
                ClaimsPrincipal principal = GetPrincipal(token);
                if (principal != null)
                {
                    try
                    {
                        ClaimsIdentity identity = principal.Identity as ClaimsIdentity;
                        Claim claim = identity?.FindFirst(ClaimTypes.Name);
                        username = claim?.Value;
                        log.Info(username != null
                            ? $"ValidateToken succeeded for username='{username}'"
                            : "ValidateToken did not find Name claim");
                    }
                    catch (Exception ex)
                    {
                        log.Error("Error extracting username claim", ex);
                    }
                }
                else
                {
                    log.Warn("GetPrincipal returned null invalidating token");
                }
            }

            if (String.IsNullOrWhiteSpace(username) && Api.IsApplicationDevMode())
            {
                username = ConfigurationHelper.Configuration["AS_API_DevModeNullUser"];
                if (!String.IsNullOrWhiteSpace(username))
                {
                    log.Warn($"API is in Dev mode and is returning a default username '{username}'");
                }
            }

            log.Info($"Exiting ValidateToken() with result {(username ?? "null")}");
            return username;
        }
    }
}
