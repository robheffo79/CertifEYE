using AnchorSafe.Data;
using System;
using System.Linq;
using log4net;
using System.Reflection;
using System.Data.Entity;
using System.Threading.Tasks;

namespace AnchorSafe.API.Services
{
    public static class UserService
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Retrieves a user by their unique identifier.
        /// </summary>
        /// <param name="userId">The user's unique identifier.</param>
        /// <returns>The <see cref="Data.Users"/> if found; otherwise null.</returns>
        public static async Task<Data.Users> GetUser(Int32 userId)
        {
            log.Info("Entering UserService.GetUser()");
            log.Debug($"Parameter userId={userId}");

            Data.Users user = null;
            try
            {
                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    db.Configuration.LazyLoadingEnabled = false;
                    user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
                    log.Debug(user != null ? $"User found: {user.Id}" : "No user found");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error in GetUser for userId={userId}", ex);
            }

            log.Info("Exiting UserService.GetUser()");
            return user;
        }

        /// <summary>
        /// Retrieves a user by their username (email).
        /// </summary>
        /// <param name="username">The user's email address.</param>
        /// <returns>The <see cref="Data.Users"/> if found; otherwise null.</returns>
        public static async Task<Data.Users> GetUserByUsername(String username)
        {
            log.Info("Entering UserService.GetUserByUsername()");
            log.Debug($"Parameter username='{username}'");

            Data.Users user = null;
            try
            {
                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    db.Configuration.LazyLoadingEnabled = false;
                    user = await db.Users.FirstOrDefaultAsync(x => x.Email == username);
                    log.Debug(user != null ? $"User found: {user.Id}" : "No user found");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error in GetUserByUsername for username='{username}'", ex);
            }

            log.Info("Exiting UserService.GetUserByUsername()");
            return user;
        }
    }

    public class LoginUser
    {
        public Int32 UserId { get; set; }
        public String Username { get; set; }
        public String Password { get; set; }
    }

    public class UpdateUser
    {
        public Int32 UserId { get; set; }
        public String Username { get; set; }
        public String Password { get; set; }
    }
}
