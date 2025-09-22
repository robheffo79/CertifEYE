using AnchorSafe.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using log4net;
using System.Reflection;
using System.Threading.Tasks;

namespace AnchorSafe.API.Helpers
{
    /// <summary>
    /// Represents application metadata.
    /// </summary>
    public class App
    {
        public string Version { get; set; }
        public string Device { get; set; }
        public string DeviceModel { get; set; }
        public string OS { get; set; }
        public string OSVersion { get; set; }
        public AnchorSafe.Data.ConnectionType ConnectionType { get; set; }
    }

    /// <summary>
    /// Provides API mode utilities.
    /// </summary>
    public static class Api
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public enum ApplicationStatus
        {
            Dev,
            Live
        }

        /// <summary>
        /// Gets the current application mode from configuration.
        /// </summary>
        public static ApplicationStatus ApplicationMode()
        {
            log.Info("Entering Api.ApplicationMode()");
            string applicationMode = ConfigurationManager.AppSettings["AS_API_ApplicationMode"];
            log.Debug($"Configuration AS_API_ApplicationMode = '{applicationMode}'");

            ApplicationStatus status = applicationMode?.ToLower().Contains("live") == true
                ? ApplicationStatus.Live
                : ApplicationStatus.Dev;

            log.Info($"Exiting Api.ApplicationMode() with result = {status}");
            return status;
        }

        /// <summary>
        /// Returns true if the application is running in dev mode.
        /// </summary>
        public static bool IsApplicationDevMode()
        {
            log.Info("Entering Api.IsApplicationDevMode()");
            bool isDev = ApplicationMode() == ApplicationStatus.Dev;
            log.Info($"Exiting Api.IsApplicationDevMode() with result = {isDev}");
            return isDev;
        }
    }

    /// <summary>
    /// Dashboard log types enumeration.
    /// </summary>
    public class Dashboard
    {
        public enum LogType
        {
            UserLogin = 1,
            UserLoginFailed = 2,
            Exception = 3,
            UserLogout = 4,
            SendSupportEmail = 5,
            SendSupportEmailError = 6,
            SyncJob = 7,
            SimProSync = 8,
            DataPull = 9
        }
    }

    /// <summary>
    /// Manages saving of device metadata.
    /// </summary>
    public class Device
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Saves device metadata, returns success flag.
        /// </summary>
        public static async Task<Boolean> SaveDeviceMeta(List<string> appHeaders, DeviceReferenceType referenceType, string referenceId, string extra = "")
        {
            log.Info("Entering Device.SaveDeviceMeta(overload)");
            bool result = false;
            string debug = string.Empty;
            try
            {
                (Boolean result, String debug) metaResult = await SaveDeviceMeta(appHeaders, referenceType, referenceId, debug, extra);
                result = metaResult.result;
                debug = metaResult.debug;
                log.Info($"Exiting Device.SaveDeviceMeta(overload) with result = {result}, debug = '{debug}'");
            }
            catch (Exception ex)
            {
                log.Error("Device.SaveDeviceMeta(overload) threw exception", ex);
            }
            return result;
        }

        /// <summary>
        /// Saves device metadata with debug output.
        /// </summary>
        public static async Task<(Boolean result, String debug)> SaveDeviceMeta(List<string> appHeaders, DeviceReferenceType referenceType, string referenceId, string debug, string extra = "")
        {
            log.Info("Entering Device.SaveDeviceMeta(headers, referenceType, referenceId, ref debug, extra)");
            log.Debug($"Headers count: {appHeaders?.Count ?? 0}, referenceType: {referenceType}, referenceId: '{referenceId}', extra: '{extra}'");

            string connHeader = string.Empty;
            string appHeader = string.Empty;
            string deviceHeader = string.Empty;

            debug = $"START ({appHeaders?.Count ?? 0} header(s)) ";

            try
            {
                App app = new App();
                connHeader = appHeaders?.ElementAtOrDefault(0) ?? string.Empty;
                appHeader = appHeaders?.ElementAtOrDefault(1) ?? string.Empty;
                deviceHeader = appHeaders?.ElementAtOrDefault(2) ?? string.Empty;

                log.Debug($"Parsed connHeader='{connHeader}', appHeader='{appHeader}', deviceHeader='{deviceHeader}'");

                // Extract connection data
                if (!string.IsNullOrEmpty(connHeader))
                {
                    string[] conns = connHeader.Split(',');
                    int maxConn = conns.Select(c => int.TryParse(c.Trim(), out int n) ? n : 0).DefaultIfEmpty(0).Max();
                    app.ConnectionType = (ConnectionType)maxConn;
                    log.Debug($"Determined ConnectionType={app.ConnectionType}");
                }

                // Extract app version
                if (!string.IsNullOrEmpty(appHeader) && appHeader.Contains("AnchorSafeApp."))
                {
                    appHeader = appHeader.Trim();
                    app.Version = appHeader.Replace("AnchorSafeApp.", "").Split('/')[1];
                    log.Debug($"Extracted AppVersion='{app.Version}'");
                }

                // Extract device info
                if (!string.IsNullOrEmpty(deviceHeader) && deviceHeader.Contains("("))
                {
                    string[] deviceInfo = deviceHeader.Trim('(', ')').Split(';');
                    if (deviceInfo.Length >= 4)
                    {
                        app.Device = deviceInfo[0].Trim();
                        app.DeviceModel = deviceInfo[1].Trim();
                        app.OS = deviceInfo[2].Trim();
                        app.OSVersion = deviceInfo[3].Trim();
                        log.Debug($"Extracted Device='{app.Device}', DeviceModel='{app.DeviceModel}', OS='{app.OS}', OSVersion='{app.OSVersion}'");
                    }
                }

                // Prepare metadata entity
                DeviceMeta meta = new DeviceMeta
                {
                    ReferenceType = referenceType.ToString(),
                    ReferenceId = referenceId,
                    AppVersion = app.Version,
                    Device = app.Device,
                    DeviceModel = app.DeviceModel,
                    Os = app.OS,
                    OsVersion = app.OSVersion,
                    Connection = app.ConnectionType.ToString(),
                    ExtraData = extra,
                    DateCreated = DateTime.Now
                };

                // Add sync/resync flag for inspection
                if (referenceType == DeviceReferenceType.Inspection)
                {
                    int inspectionId = await Helpers.Inspection.InspectionExists(referenceId);
                    meta.ExtraData += inspectionId > 0 ? "Resync" : "Sync";
                    log.Debug($"Appended inspection sync flag to ExtraData: '{meta.ExtraData}'");
                }

                // Persist
                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    log.Debug("Saving DeviceMeta to database");
                    db.Entry(meta).State = System.Data.Entity.EntityState.Added;
                    await db.SaveChangesAsync();
                }

                debug += " END |";
                log.Info($"Exiting Device.SaveDeviceMeta(...) with success. debug='{debug}'");
                return (true, debug);
            }
            catch (Exception ex)
            {
                log.Error($"SaveDeviceMeta | Failed saving device meta data. connHeader='{connHeader}', appHeader='{appHeader}', deviceHeader='{deviceHeader}'", ex);
            }

            log.Info("Exiting Device.SaveDeviceMeta(...) with failure");
            return (false, debug);
        }
    }
}
