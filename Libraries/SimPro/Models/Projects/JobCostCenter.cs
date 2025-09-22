using AnchorSafe.SimPro.DTO.Models.People;
using System;

namespace AnchorSafe.SimPro.DTO.Models.Projects
{
    public class JobCostCenter
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
        public bool ItemsLocked { get; set; }
        /*public List<VendorOrder> VendorOrders { get; set; }
        public Total Total { get; set; }
        public Claimed Claimed { get; set; }
        public Totals Totals { get; set; }*/
        public string DateModified { get; set; }
        public DateTime _DateModified { get { return Helpers.Modifiers.GetDate(this.DateModified); } }
        public int PercentComplete { get; set; }
    }

    public class CostCenter
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }
}
