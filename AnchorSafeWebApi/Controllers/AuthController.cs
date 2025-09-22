using AnchorSafe.API.Helpers;
using AnchorSafe.API.Services;
using AnchorSafe.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace AnchorSafe.API.Controllers
{
    [AllowAnonymous]
    public class AuthController : ApiController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Int32 ExpiryDuration = -1;    // In minutes

        public AuthController()
        {
            ExpiryDuration = Int32.Parse(ConfigurationManager.AppSettings["AS_API_TokenExpiryDuration"]);
        }

        /// <summary>
        /// Simple hello endpoint to verify service availability.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IHttpActionResult Hello()
        {
            log.Info("Entering Hello()");
            log.Info("Hello | User: Anon | Details: Hello Auth :)");
            log.Info("Exiting Hello()");
            return Ok("Hi There Auth");
        }

        /// <summary>
        /// Test endpoint that validates token from header.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> Test()
        {
            log.Info("Entering Test()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug($"Test | Retrieved token: {token}");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Test | Validated token. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("Test | Authentication failed. Access Denied.");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting Test() with Forbidden.");
                return forbidden;
            }

            if (username == null)
            {
                log.Warn("Test | Invalid user data.");
                IHttpActionResult badRequest = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Invalid user data." }));
                log.Info("Exiting Test() with BadRequest.");
                return badRequest;
            }

            Users user;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                log.Debug($"Test | Looking up user by email: {username}");
                user = await db.Users.FirstOrDefaultAsync(x => x.Email == username);
            }

            log.Info($"Test | User: {user.Id} | Details: Tested user {user.Email}");
            log.Info("Exiting Test() with Ok.");
            return Ok($"User: {user.FirstName} {user.Surname}");
        }

        /// <summary>
        /// Registers a new user. Not implemented.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public IHttpActionResult Register([FromBody] Users user)
        {
            log.Info("Entering Register()");
            log.Warn("Register() not implemented.");
            log.Info("Exiting Register()");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Authenticates the user and returns a token.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IHttpActionResult> Login([FromBody] LoginUser login)
        {
            log.Info("Entering Login()");
            if (login == null)
            {
                log.Warn("Login | No credentials provided.");
                IHttpActionResult badRequest = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No credentials." }));
                log.Info("Exiting Login() with BadRequest.");
                return badRequest;
            }
            log.Debug($"Login | Received login request for Username: {login.Username}");

            if (Request.Headers.TryGetValues("X-CPro", out IEnumerable<String> connHeaders) &&
                Request.Headers.TryGetValues("User-Agent", out IEnumerable<String> userAgentHeaders))
            {
                log.Debug("Login | Saving device metadata.");
                await Device.SaveDeviceMeta(connHeaders.Union(userAgentHeaders).ToList(), DeviceReferenceType.User, login.Username, "Login");
            }

            Security sec = new Security();
            sec.Init();
            log.Debug("Login | Security initialized.");

            ASLogs asLog = new ASLogs();
            Boolean result = false;
            String token = String.Empty;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                log.Debug($"Login | Looking up authorised user by email: {login.Username}");
                Users user = await db.Users.FirstOrDefaultAsync(x => x.Email == login.Username && x.IsAuthorised);
                if (user != null)
                {
                    String pwHash = await db.Database.SqlQuery<String>("SELECT TOP(1) Password FROM Users WHERE Id = @p0;", user.Id).FirstOrDefaultAsync();
                    log.Debug("Login | Retrieved password hash from database.");
                    result = sec.IsValid(login.Password, pwHash);
                    log.Debug($"Login | Password validation result: {result}");

                    if (result)
                    {
                        token = TokenService.GenerateToken(user.Email, ExpiryDuration);
                        log.Debug("Login | Token generated.");
                    }
                    else
                    {
                        log.Warn($"Login | User: {user.Id} | Details: Invalid credentials for {user.Email}");
                        asLog.AddLogEntry($"Failed User Login (App) - Invalid credentials for {user.Email}", (Int32)Dashboard.LogType.UserLoginFailed, user.Id);
                        IHttpActionResult badCred = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Invalid credentials." }));
                        log.Info("Exiting Login() with BadRequest (Invalid credentials).");
                        return badCred;
                    }

                    log.Debug($"Login | Token expiry duration set to {ExpiryDuration} minutes.");

                    log.Info($"Login | User: {user.Id} | Details: {user.Email}");
                    asLog.AddLogEntry($"User Login (App) - {user.Email}", (Int32)Dashboard.LogType.UserLogin, user.Id);

                    log.Info("Exiting Login() with Ok.");
                    return Ok(new
                    {
                        Auth = result,
                        Token = token,
                        UserId = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.Surname,
                        Expiry = DateTime.Now.AddMinutes(ExpiryDuration),
                        Debug = sec.Debug
                    });
                }
                else
                {
                    log.Warn($"Login | User: {login.UserId} | Details: Invalid user, {login.Username}");
                    asLog.AddLogEntry($"Failed User Login (App) - Invalid user, {login.Username}", (Int32)Dashboard.LogType.UserLoginFailed, login.UserId);
                    IHttpActionResult badUser = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Invalid user." }));
                    log.Info("Exiting Login() with BadRequest (Invalid user).");
                    return badUser;
                }
            }
        }

        /// <summary>
        /// DEBUG ONLY: Easily update user password. REMOVE FOR PRODUCTION.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> EasyUpdatePassword(Int32 id, String pw)
        {
            log.Info("Entering EasyUpdatePassword()");
            if (!Api.IsApplicationDevMode())
            {
                log.Warn("EasyUpdatePassword | Unavailable. Not in dev mode.");
                IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Unavailable." }));
                log.Info("Exiting EasyUpdatePassword() with BadRequest.");
                return bad;
            }

            Security sec = new Security();
            sec.Init();
            log.Debug("EasyUpdatePassword | Security initialized.");

            String passHash = sec.GeneratePasswordHash(pw);
            log.Debug("EasyUpdatePassword | Generated password hash.");

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                Users user = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.IsAuthorised);
                if (user != null)
                {
                    await db.Database.ExecuteSqlCommandAsync("UPDATE Users SET Password = @p0 WHERE Id = @p1;", passHash, user.Id);
                    log.Info($"EasyUpdatePassword | User: {user.Id} | Details: {user.Email} updated password");
                }
                else
                {
                    log.Warn($"EasyUpdatePassword | Invalid user id: {id}");
                    IHttpActionResult badUser = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Invalid user." }));
                    log.Info("Exiting EasyUpdatePassword() with BadRequest (Invalid user).");
                    return badUser;
                }
            }

            log.Info("Exiting EasyUpdatePassword() with Ok.");
            return Ok(new { Msg = "Password Updated", Debug = sec.Debug });
        }

        /// <summary>
        /// Updates password for authenticated user.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> UpdatePassword([FromBody] UpdateUser updateUser)
        {
            log.Info("Entering UpdatePassword()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug($"UpdatePassword | Retrieved token: {token}");
            String username = TokenService.ValidateToken(token);
            log.Debug($"UpdatePassword | Validated token. Username: {username}");

            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("UpdatePassword | Authentication failed. Access Denied.");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting UpdatePassword() with Forbidden.");
                return forbidden;
            }

            Security sec = new Security();
            sec.Init();
            log.Debug("UpdatePassword | Security initialized.");

            String passHash = sec.GeneratePasswordHash(updateUser.Password);
            log.Debug("UpdatePassword | Generated password hash.");

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                Users user = await db.Users.FirstOrDefaultAsync(x => x.Email == updateUser.Username && x.IsAuthorised);
                if (user != null)
                {
                    await db.Database.ExecuteSqlCommandAsync("UPDATE Users SET Password = @p0 WHERE Id = @p1;", passHash, user.Id);
                    log.Info($"UpdatePassword | User: {user.Id} | Details: {user.Email} updated password");
                }
                else
                {
                    log.Warn($"UpdatePassword | Invalid user: {updateUser.Username}");
                    IHttpActionResult badUser = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Invalid user." }));
                    log.Info("Exiting UpdatePassword() with BadRequest (Invalid user).");
                    return badUser;
                }
            }

            log.Info("Exiting UpdatePassword() with Ok.");
            return Ok(new { Msg = "Password Updated", Debug = sec.Debug });
        }

        /// <summary>
        /// Validates the provided token and returns OK if valid.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> Validate()
        {
            log.Info("Entering Validate()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug($"Validate | Retrieved token: {token}");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Validate | Validated token. Username: {username}");

            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("Validate | Authentication failed. Access Denied.");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting Validate() with Forbidden.");
                return forbidden;
            }

            Int32 userId = -1;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                Users user = await db.Users.FirstOrDefaultAsync(x => x.Email == username && x.IsAuthorised);
                if (user != null)
                {
                    userId = user.Id;
                    log.Debug($"Validate | Found user: {user.Email} with ID {userId}");
                    String tokenUsername = TokenService.ValidateToken(token);
                    if (username.Equals(tokenUsername))
                    {
                        log.Info($"Validate | User: {user.Id} | Details: {user.Email}");
                        log.Info("Exiting Validate() with Ok.");
                        return Ok("OK");
                    }
                    else
                    {
                        log.Warn("Validate | Invalid token content.");
                        IHttpActionResult badToken = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Invalid token." }));
                        log.Info("Exiting Validate() with BadRequest (Invalid token).");
                        return badToken;
                    }
                }
                else
                {
                    log.Warn($"Validate | Invalid user: {username}");
                    IHttpActionResult badUser = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Invalid user." }));
                    log.Info("Exiting Validate() with BadRequest (Invalid user).");
                    return badUser;
                }
            }
        }
    }
}
