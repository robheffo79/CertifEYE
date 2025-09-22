using AnchorSafe.SimPro.DTO.Models.People;
using System;

namespace AnchorSafe.SimPro.DTO.Models.Projects
{
    public class QuoteCostCenter
    {
        public int ID { get; set; }
        public CostCenter CostCenter { get; set; }
        public int JobID { get; set; }
        public string Name { get; set; }
        public string Header { get; set; }
        public Site Site { get; set; }
        public string Stage { get; set; } // "Declined", "AwaitingApproval", "Pending", "InProgress", "Complete", "Invoiced"
        public string Description { get; set; }
        public string Notes { get; set; }
        public string OrderNo { get; set; }
        public string StartDate { get; set; }
        public DateTime _StartDate { get { return Helpers.Modifiers.GetDate(this.StartDate); } }
        public string EndDate { get; set; }
        public DateTime _EndDate { get { return Helpers.Modifiers.GetDate(this.EndDate); } }
        public bool AutoAdjustDates { get; set; }
        public int DisplayOrder { get; set; }
        public bool Variation { get; set; }
        public string VariationApprovalDate { get; set; }
        public DateTime _VariationApprovalDate { get { return Helpers.Modifiers.GetDate(this.VariationApprovalDate); } }
        public bool OptionalDepartment { get; set; }
        public bool ItemsLocked { get; set; }
        /*public Total Total { get; set; }
        public Totals Totals { get; set; }*/
        public string DateModified { get; set; }
        public DateTime _DateModified { get { return Helpers.Modifiers.GetDate(this.DateModified); } }
    }

}
