/* Data Extensions */
using System;

namespace AnchorSafe.Data
{
    public class AppInspection : Inspections
    {
        public int ServerInspectionId { get; set; }
        public string LocationName { get; set; }
        public string SiteName { get; set; }

        /*public AppInspection(Data.Inspections inspection)
        {
            Id = inspection.Id;
            ClientId = inspection.ClientId;
            CreatedUserId = inspection.CreatedUserId;
            DateCreated = inspection.DateCreated;
            DateModified = inspection.DateModified;
            InspectionDate = inspection.InspectionDate;
            InspectionStatusId = inspection.InspectionStatusId;
            InspectionTypeId = inspection.InspectionTypeId;
            IsLocked = inspection.IsLocked;
            Latitude = inspection.Latitude;
            Longitude = inspection.Longitude;
            LocationId = inspection.LocationId;
            ModifiedUserId = inspection.ModifiedUserId;
            SimProId = inspection.SimProId;
            SiteId = inspection.SiteId;
            UserId = inspection.UserId;
            SyncDate = inspection.SyncDate;

            InspectionItems = inspection.InspectionItems;
            Sites = inspection.Sites;
            Users = inspection.Users;
            Locations = inspection.Locations;
            InspectionType = inspection.InspectionType;
            InspectionStatus = inspection.InspectionStatus;
        }*/

    }

    public class DataDump
    {
        public string UserDisplayName { get; set; }
        public int UserId { get; set; }
        public long FileSize { get; set; }
        public DateTime DateCreated { get; set; }
        public string Link { get; set; }
    }
}