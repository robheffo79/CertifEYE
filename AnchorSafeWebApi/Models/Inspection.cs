using System;
using System.Collections.Generic;
using System.Linq;

namespace AnchorSafe.API.Models.DTO
{
    public enum InspectionStatus
    {
        Unassigned = 1,
        InProgress,
        Completed,
        Archived,
        Invoiced,
        Queued
    }

    public enum InspectionItemStatus
    {
        Compliant = 1,
        NonCompliant,
        NA,
    }

    public enum InspectionType
    {
        Unassigned = 0,
        Installation = 1,
        Recertification
    }

    public class Inspection
    {
        public int? SimProId { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTime DateCreated { get; set; }
        public int? ModifiedUserId { get; set; }
        public DateTime DateModified { get; set; }
        public string Longitude { get; set; }
        public string Latitude { get; set; }
        public int UserId { get; set; }
        public DateTime? SyncDate { get; set; }
        public bool IsLocked { get; set; }
        public int? InspectionStatusId { get; set; }
        public int InspectionTypeId { get; set; } = (int)InspectionType.Unassigned;
        public DateTime? InspectionDate { get; set; }
        public int? LocationId { get; set; }
        public int? SiteId { get; set; }
        public int ClientId { get; set; }
        public int Id { get; set; }
        public string Nonce { get; set; }

        public List<InspectionItem> InspectionItems { get; set; }

        public Inspection() { }

        public Inspection(Data.Inspections inspection)
        {
            SimProId = inspection.SimProId;
            CreatedUserId = inspection.CreatedUserId;
            DateCreated = inspection.DateCreated;
            ModifiedUserId = inspection.ModifiedUserId;
            DateModified = inspection.DateModified;
            Longitude = inspection.Longitude;
            Latitude = inspection.Latitude;
            UserId = inspection.UserId;
            SyncDate = inspection.SyncDate;
            IsLocked = inspection.IsLocked;
            InspectionStatusId = inspection.InspectionStatusId;
            InspectionTypeId = inspection?.InspectionTypeId ?? (int)InspectionType.Unassigned;
            InspectionDate = inspection.InspectionDate;
            LocationId = inspection.LocationId;
            SiteId = inspection.SiteId;
            ClientId = inspection.ClientId;
            Id = inspection.Id;
            Nonce = inspection.Nonce.ToString();

            InspectionItems = new List<InspectionItem>();
            if (inspection.InspectionItems.Any())
            {
                foreach (Data.InspectionItems item in inspection.InspectionItems) { InspectionItems.Add(new InspectionItem(item)); }
            }
        }
    }

    public class InspectionItem
    {
        public int Id { get; set; }
        public int InspectionId { get; set; }
        public int CategoryId { get; set; }
        public int InspectionItemStatusId { get; set; }
        public string Findings { get; set; }
        public string Recommendations { get; set; }
        public bool IsSynced { get; set; }
        public int ListingOrder { get; set; }
        public string Nonce { get; set; }

        public InspectionItem() { }

        public InspectionItem(Data.InspectionItems item)
        {
            Id = item.Id;
            InspectionId = item.InspectionId;
            CategoryId = item.CategoryId;
            InspectionItemStatusId = item.InspectionItemStatusId;
            Findings = item.Findings;
            Recommendations = item.Recommendations;
            IsSynced = item.IsSynced;
            ListingOrder = item.ListingOrder;
        }
    }

    // API
    public class InspectionAssignment
    {
        public int InspectionId { get; set; }
        public int UserId { get; set; }
        public DateTime? DateStarted { get; set; }

    }

    public class AppInspectionSync
    {
        public int InspectionId { get; set; }
        public int ServerInspectionId { get; set; }
        public int ItemCount { get; set; }
        public int ItemMediaCount { get; set; }
    }

}