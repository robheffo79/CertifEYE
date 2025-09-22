using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace AnchorSafe.SimPro.DTO.Models.Projects
{
    public enum JobStageEnum
    {
        [Description("Unassigned")]
        Pending,
        [Description("In Progress")]
        Progress,
        [Description("Completed")]
        Complete,
        Invoiced,
        Archived
    }

    public class JobContainer : SimpleContainer<JobListItem> { }

    public class JobListItem : SimpleItem, IEquatable<JobListItem>
    {
        public string Description { get; set; }
        public Total Total { get; set; }
        // Extended
        public string Name { get; set; }
        public string Type { get; set; }
        public string Stage { get; set; }
        public Status Status { get; set; }
        public People.SimpleCustomer Customer { get; set; }
        public People.Site Site { get; set; }
        public string CompletedDate { get; set; }

        public bool Equals(JobListItem other)
        {
            if (Object.ReferenceEquals(this, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID) && Name.Equals(other.Name);
        }
        public override int GetHashCode()
        {
            //Get hash code for the Name field if it is not null.
            int hashName = Name == null ? 0 : Name.GetHashCode();

            //Get hash code for the ID field.
            int hashID = ID.GetHashCode();

            //Calculate the hash code for the item.
            return hashName ^ hashID;
        }
    }

    public class Job
    {
        public int ID { get; set; }
        public string Type { get; set; }
        public People.SimpleCustomer Customer { get; set; }
        public People.CustomerContact CustomerContact { get; set; }
        public List<People.SimpleContact> AdditionalContacts { get; set; }
        public People.Site Site { get; set; }
        public People.SiteContact SiteContact { get; set; }
        public string OrderNo { get; set; }
        public string RequestNo { get; set; } // Depreciated
        public string Name { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public string DateIssued { get; set; } // YYYY-MM-DD
        public DateTime _DateIssued { get { return Helpers.Modifiers.GetDate(this.DateIssued); } }
        public string DueDate { get; set; } // YYYY-MM-DD
        public DateTime _DueDate { get { return Helpers.Modifiers.GetDate(this.DueDate); } }
        public string DueTime { get; set; } // HH:MM
        public List<Setup.Tag> Tags { get; set; }
        public People.Salesperson Salesperson { get; set; }
        public People.ProjectManager ProjectManager { get; set; }
        public People.Technician Technician { get; set; }
        public string Stage { get; set; }
        public Status Status { get; set; }
        public ResponseTime ResponseTime { get; set; }
        public bool IsVariation { get; set; }
        public List<Object> LinkedVariations { get; set; }
        public ConvertedFromQuote ConvertedFromQuote { get; set; }
        public string DateModified { get; set; }
        public DateTime _DateModified { get { return Helpers.Modifiers.GetDate(this.DateModified); } }
        public bool AutoAdjustStatus { get; set; }
        public Total Total { get; set; }
        public Totals Totals { get; set; }
        public List<Object> CustomFields { get; set; }
        public STC STC { get; set; }
        public string CompletedDate { get; set; }
        public DateTime _CompletedDate { get { return Helpers.Modifiers.GetDate(this.CompletedDate); } }

        public List<JobSection> Sections { get; set; }
    }


    public class JobSectionContainer : SimpleContainer<JobSectionListItem> { }

    public class JobSectionListItem : SimpleItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int DisplayOrder { get; set; }
        // Extended
        public int JobID { get; set; }
    }

    public class JobSection
    {
        public int ID { get; set; }
        public int JobID { get; set; }
        public int SiteID { get; set; }
    }

}