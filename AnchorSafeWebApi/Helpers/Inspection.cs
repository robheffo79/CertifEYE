using AnchorSafe.Data;
using System;
using System.Linq;
using log4net;
using System.Reflection;
using System.Data.Entity;
using System.Threading.Tasks;

namespace AnchorSafe.API.Helpers
{
    public class Inspection
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Checks whether an inspection with the given nonce exists.
        /// Returns count (0 if none), or –1 on invalid input.
        /// </summary>
        public static async Task<Int32> InspectionExists(String nonce)
        {
            log.Info("Entering InspectionExists()");
            log.Debug($"Parameter nonce='{nonce}'");

            int result = -1;
            if (string.IsNullOrEmpty(nonce))
            {
                log.Warn("InspectionExists | nonce is null or empty");
                log.Info("Exiting InspectionExists() with -1");
                return result;
            }

            if (!Guid.TryParse(nonce, out Guid temp))
            {
                log.Warn($"InspectionExists | invalid GUID: '{nonce}'");
                log.Info("Exiting InspectionExists() with -1");
                return result;
            }

            try
            {
                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    log.Debug("InspectionExists | querying database");
                    result = await db.Inspections.CountAsync(x => x.Nonce.ToString().Equals(nonce, StringComparison.OrdinalIgnoreCase));
                    log.Info($"InspectionExists | found {result} record(s)");
                }
            }
            catch (Exception ex)
            {
                log.Error("InspectionExists | error querying database", ex);
                result = -1;
            }

            log.Info($"Exiting InspectionExists() with {result}");
            return result;
        }

        /// <summary>
        /// Checks whether an inspection item with the given nonce exists.
        /// Returns count, or –1 on error.
        /// </summary>
        public static async Task<Int32> InspectionItemExists(string nonce)
        {
            log.Info("Entering InspectionItemExists()");
            log.Debug($"Parameter nonce='{nonce}'");

            int result = -1;
            try
            {
                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    log.Debug("InspectionItemExists | querying database");
                    result = await db.InspectionItems.CountAsync(x => x.Nonce.ToString().Equals(nonce, StringComparison.OrdinalIgnoreCase));
                    log.Info($"InspectionItemExists | found {result} record(s)");
                }
            }
            catch (Exception ex)
            {
                log.Error("InspectionItemExists | error querying database", ex);
                result = -1;
            }

            log.Info($"Exiting InspectionItemExists() with {result}");
            return result;
        }
    }
}
