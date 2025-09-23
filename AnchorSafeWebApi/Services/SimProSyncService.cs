using AnchorSafe.API.Helpers;
using AnchorSafe.SimPro;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using System.Reflection;

namespace AnchorSafe.API.Services
{
    /// <summary>
    /// Service to kick off and track SimPro data synchronization jobs.
    /// </summary>
    public class SimProSyncService
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ConcurrentDictionary<Guid, SimProSyncJob> _runningTasks = new ConcurrentDictionary<Guid, SimProSyncJob>();

        /// <summary>
        /// Starts a synchronization job for the specified tables.
        /// </summary>
        /// <param name="tables">The tables to sync.</param>
        /// <returns>The unique job identifier.</returns>
        public Guid StartSync(SimProTable tables)
        {
            log.Info("Entering SimProSyncService.StartSync()");
            log.Debug($"Requested tables: {tables}");

            SimProSyncJob syncJob = new SimProSyncJob();
            Guid taskId = syncJob.TaskId;
            _runningTasks[taskId] = syncJob;
            log.Info($"Registered new sync job with TaskId {taskId}");

            try
            {
                syncJob.Start(tables);
                log.Info($"Sync job {taskId} started");
            }
            catch (Exception ex)
            {
                log.Error($"Error starting sync job {taskId}", ex);
                throw;
            }

            log.Info($"Exiting SimProSyncService.StartSync() with TaskId {taskId}");
            return taskId;
        }

        /// <summary>
        /// Internal synchronous sync method (not implemented).
        /// </summary>
        private void StartSyncInternal(SimProTable tables)
        {
            log.Info("Entering SimProSyncService.StartSyncInternal()");
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Represents a background synchronization job for SimPro data.
    /// </summary>
    public class SimProSyncJob
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<string> _messages = new List<string>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _syncTask;

        internal SimProSyncJob()
        {
            log.Info("Creating new SimProSyncJob instance");
        }

        /// <summary>
        /// Unique identifier for this job.
        /// </summary>
        public Guid TaskId { get; } = Guid.NewGuid();

        /// <summary>
        /// When the job started execution.
        /// </summary>
        public DateTime? JobStarted { get; private set; }

        /// <summary>
        /// When the job completed execution.
        /// </summary>
        public DateTime? JobCompleted { get; private set; }

        /// <summary>
        /// Current state of the job.
        /// </summary>
        public JobState State { get; private set; } = JobState.Pending;

        /// <summary>
        /// Messages logged during the job.
        /// </summary>
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToArray();
                }
            }
        }

        /// <summary>
        /// Appends a message to the job log.
        /// </summary>
        internal void AppendMessage(string message)
        {
            log.Debug($"SimProSyncJob.AppendMessage: {message}");
            lock (_messages)
            {
                _messages.Add(message);
            }
        }

        /// <summary>
        /// Starts the job asynchronously for the given tables.
        /// </summary>
        internal void Start(SimProTable tables)
        {
            log.Info($"SimProSyncJob {TaskId}: Starting job with tables {tables}");
            _syncTask = Task.Factory.StartNew(() =>
            {
                JobStarted = DateTime.Now;
                State = JobState.Running;
                log.Info($"SimProSyncJob {TaskId}: JobStarted at {JobStarted}");

                AppendMessage("Starting sync process");
                try
                {
                    PerformSync(tables);
                }
                catch (Exception ex)
                {
                    AppendMessage(ex.ToString());
                    State = JobState.Failed;
                    log.Error($"SimProSyncJob {TaskId}: Job failed", ex);
                }
                finally
                {
                    JobCompleted = DateTime.Now;
                    if (State != JobState.Failed)
                    {
                        State = JobState.Complete;
                        log.Info($"SimProSyncJob {TaskId}: Job completed successfully at {JobCompleted}");
                    }
                    else
                    {
                        log.Info($"SimProSyncJob {TaskId}: Job marked as failed at {JobCompleted}");
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Performs the synchronization for specified tables.
        /// </summary>
        private void PerformSync(SimProTable tables)
        {
            log.Info($"SimProSyncJob {TaskId}: Entering PerformSync");
            IConfiguration configuration = ConfigurationHelper.Configuration;
            SimProSettings simProSettings = new SimProSettings
            {
                Host = configuration["SimPro_API_BaseUrl"] ?? string.Empty,
                Version = configuration["SimPro_API_Version"] ?? string.Empty,
                Key = configuration["SimPro_API_Key"] ?? string.Empty,
                CompanyId = configuration.GetValue<int>("SimPro_API_CompanyId"),
                CachePath = configuration["SimPro_API_CachePath"] ?? string.Empty
            };

            if (tables.HasFlag(SimProTable.Clients))
            {
                AppendMessage("Performing sync of Clients");
                log.Info($"SimProSyncJob {TaskId}: Syncing Clients");
                SyncClients(simProSettings);
            }

            if (tables.HasFlag(SimProTable.Sites))
            {
                AppendMessage("Performing sync of Sites");
                log.Info($"SimProSyncJob {TaskId}: Syncing Sites");
                SyncSites(simProSettings);
            }

            if (tables.HasFlag(SimProTable.Locations))
            {
                AppendMessage("Performing sync of Locations");
                log.Info($"SimProSyncJob {TaskId}: Syncing Locations");
                SyncLocations(simProSettings);
            }

            if (tables.HasFlag(SimProTable.Inspections))
            {
                AppendMessage("Performing sync of Inspections");
                log.Info($"SimProSyncJob {TaskId}: Syncing Inspections");
                SyncInspections(simProSettings);
            }
            log.Info($"SimProSyncJob {TaskId}: Exiting PerformSync");
        }

        /// <summary>
        /// Synchronizes Clients data. (Not implemented)
        /// </summary>
        private void SyncClients(SimProSettings simProSettings)
        {
            log.Info($"SimProSyncJob {TaskId}: Entering SyncClients");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Synchronizes Sites data. (Not implemented)
        /// </summary>
        private void SyncSites(SimProSettings simProSettings)
        {
            log.Info($"SimProSyncJob {TaskId}: Entering SyncSites");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Synchronizes Locations data. (Not implemented)
        /// </summary>
        private void SyncLocations(SimProSettings simProSettings)
        {
            log.Info($"SimProSyncJob {TaskId}: Entering SyncLocations");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Synchronizes Inspections data. (Not implemented)
        /// </summary>
        private void SyncInspections(SimProSettings simProSettings)
        {
            log.Info($"SimProSyncJob {TaskId}: Entering SyncInspections");
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Represents state of a sync job.
    /// </summary>
    public enum JobState
    {
        Pending,
        Running,
        Complete,
        Failed
    }

    /// <summary>
    /// Flags for SimPro tables to sync.
    /// </summary>
    [Flags]
    public enum SimProTable
    {
        None = 0,
        Clients = 1,
        Sites = 2,
        Locations = 4,
        Inspections = 8,
        All = Clients | Sites | Locations | Inspections
    }
}
