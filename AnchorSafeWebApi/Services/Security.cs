using AnchorSafe.API.Helpers;
using Microsoft.Extensions.Configuration;
using System;
using System.Security.Cryptography;
using log4net;
using System.Reflection;

namespace AnchorSafe.API.Services
{
    /// <summary>
    /// Provides password hashing and validation services.
    /// </summary>
    public class Security : ISecurity
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //public string Pepper { get; set; }
        public String Debug { get; set; }
        private Int32 SaltByteLength { get; set; }
        private Int32 DerivedKeyLength { get; set; }
        private Int32 InterationCount { get; set; }
        private Boolean Initiated { get; set; }

        /// <summary>
        /// Initializes cryptographic parameters from configuration.
        /// </summary>
        public void Init()
        {
            log.Info("Entering Security.Init()");
            try
            {
                IConfiguration configuration = ConfigurationHelper.Configuration;
                SaltByteLength = configuration.GetValue<int>("AS_API_SecSaltByteLength");
                DerivedKeyLength = configuration.GetValue<int>("AS_API_SecDerivedKeyLength");
                InterationCount = configuration.GetValue<int>("AS_API_SecIterationCount");
                Initiated = true;
                log.Debug($"Loaded settings: SaltByteLength={SaltByteLength}, DerivedKeyLength={DerivedKeyLength}, IterationCount={InterationCount}");
                log.Info("Exiting Security.Init() successfully");
            }
            catch (Exception ex)
            {
                log.Error("Error during Security.Init()", ex);
                throw;
            }
        }

        /// <summary>
        /// Validates a password attempt against the stored hash.
        /// </summary>
        /// <param name="pwAttempt">The password attempt.</param>
        /// <param name="pwHash">The stored password hash.</param>
        /// <returns>True if valid; otherwise false.</returns>
        public Boolean IsValid(String pwAttempt, String pwHash)
        {
            log.Info("Entering Security.IsValid()");
            Boolean result = VerifyPassword(pwAttempt, pwHash);
            log.Info($"Exiting Security.IsValid() with result={result}");
            return result;
        }

        /// <summary>
        /// Generates a hashed password string.
        /// </summary>
        /// <param name="password">The plaintext password.</param>
        /// <returns>Base64-encoded hash string including salt and iteration count.</returns>
        public String GeneratePasswordHash(String password)
        {
            log.Info("Entering Security.GeneratePasswordHash()");
            if (!Initiated)
            {
                log.Error("GeneratePasswordHash called before Init()");
                throw new Exception("Security not initiated");
            }

            Byte[] salt = GenerateRandomSalt();
            log.Debug($"Generated random salt of length {salt.Length}");

            Byte[] hashValue = GenerateHashValue(password, salt, InterationCount);
            log.Debug($"Generated hash value of length {hashValue.Length}");

            Byte[] iterationCountBtyeArr = BitConverter.GetBytes(InterationCount);
            Int32 totalLength = SaltByteLength + DerivedKeyLength + iterationCountBtyeArr.Length;
            Byte[] valueToSave = new Byte[totalLength];

            Buffer.BlockCopy(salt, 0, valueToSave, 0, SaltByteLength);
            Buffer.BlockCopy(hashValue, 0, valueToSave, SaltByteLength, DerivedKeyLength);
            Buffer.BlockCopy(iterationCountBtyeArr, 0, valueToSave, SaltByteLength + DerivedKeyLength, iterationCountBtyeArr.Length);

            String result = Convert.ToBase64String(valueToSave);
            log.Info("Exiting Security.GeneratePasswordHash()");
            return result;
        }

        /* Private functions */

        private Byte[] GenerateRandomSalt()
        {
            log.Info("Entering Security.GenerateRandomSalt()");
            if (!Initiated)
            {
                log.Error("GenerateRandomSalt called before Init()");
                throw new Exception("Security not initiated");
            }
            using (RNGCryptoServiceProvider csprng = new RNGCryptoServiceProvider())
            {
                Byte[] salt = new Byte[SaltByteLength];
                csprng.GetBytes(salt);
                log.Debug($"Random salt generated (length {salt.Length})");
                log.Info("Exiting Security.GenerateRandomSalt()");
                return salt;
            }
        }

        private Byte[] GenerateHashValue(String password, Byte[] salt, Int32 iterationCount)
        {
            log.Info("Entering Security.GenerateHashValue()");
            if (!Initiated)
            {
                log.Error("GenerateHashValue called before Init()");
                throw new Exception("Security not initiated");
            }
            String valueToHash = String.IsNullOrEmpty(password) ? String.Empty : password;
            log.Debug($"Generating hash for password (length {valueToHash.Length}) with iterationCount={iterationCount}");

            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(valueToHash, salt, iterationCount))
            {
                Byte[] hashValue = pbkdf2.GetBytes(DerivedKeyLength);
                log.Debug($"Derived key generated (length {hashValue.Length})");
                log.Info("Exiting Security.GenerateHashValue()");
                return hashValue;
            }
        }

        private Boolean VerifyPassword(String passwordGuess, String actualSavedHashResults)
        {
            log.Info("Entering Security.VerifyPassword()");
            if (!Initiated)
            {
                log.Error("VerifyPassword called before Init()");
                throw new Exception("Security not initiated");
            }

            Byte[] salt = new Byte[SaltByteLength];
            Byte[] actualPasswordByteArr = new Byte[DerivedKeyLength];
            Byte[] savedHashBytes = Convert.FromBase64String(actualSavedHashResults);

            Int32 iterationCountLength = savedHashBytes.Length - (salt.Length + actualPasswordByteArr.Length);
            Byte[] iterationCountByteArr = new Byte[iterationCountLength];

            Buffer.BlockCopy(savedHashBytes, 0, salt, 0, SaltByteLength);
            Buffer.BlockCopy(savedHashBytes, SaltByteLength, actualPasswordByteArr, 0, actualPasswordByteArr.Length);
            Buffer.BlockCopy(savedHashBytes, SaltByteLength + actualPasswordByteArr.Length, iterationCountByteArr, 0, iterationCountLength);

            Int32 iterations = BitConverter.ToInt32(iterationCountByteArr, 0);
            log.Debug($"Extracted salt length {salt.Length}, hash length {actualPasswordByteArr.Length}, iterations {iterations}");

            Byte[] passwordGuessByteArr = GenerateHashValue(passwordGuess, salt, iterations);
            Boolean result = ConstantTimeComparison(passwordGuessByteArr, actualPasswordByteArr);

            log.Info($"Exiting Security.VerifyPassword() with result={result}");
            return result;
        }

        private static Boolean ConstantTimeComparison(Byte[] passwordGuess, Byte[] actualPassword)
        {
            log.Info("Entering Security.ConstantTimeComparison()");
            UInt32 difference = (UInt32)passwordGuess.Length ^ (UInt32)actualPassword.Length;
            log.Debug($"Initial length difference value={difference}");

            Int32 length = Math.Min(passwordGuess.Length, actualPassword.Length);
            for (Int32 i = 0; i < length; i++)
            {
                difference |= (UInt32)(passwordGuess[i] ^ actualPassword[i]);
            }

            Boolean isEqual = difference == 0;
            log.Info($"Exiting Security.ConstantTimeComparison() with result={isEqual}");
            return isEqual;
        }
    }
}
