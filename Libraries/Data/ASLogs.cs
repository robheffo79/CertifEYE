using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnchorSafe.Data
{
    public class ASLogs
    {

        public string AddLogEntry(string message, int LogTypeId, int iUserId)
        {

            using (var db = new AnchorSafe_DbContext())
            {
                var logs = new Logs()
                {
                    Description = message,
                    LogTypeId = LogTypeId,
                    UserId = iUserId
                };

                db.Entry(logs).State = System.Data.Entity.EntityState.Added;

                db.SaveChanges();

                return "added";

            }

        }

        

    }

    public class CustomizedLog
    {
        public string LogType { get; set; }
        public DateTime LogDate { get; set; }
        public string Description { get; set; }
        public string UserName { get; set; }

    }

    public enum InspectionStatusLookup
    {
        Unassigned = 1,
        InProgress = 2,
        Completed = 3,
        Archived = 4,
        Invoiced = 5,
        Matched = 6
    }
}
