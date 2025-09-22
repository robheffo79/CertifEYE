using System;
using System.Collections.Generic;

namespace AnchorSafe.SimPro.DTO.Models.People
{
    public class Contact
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string GivenName { get; set; }
        public string FamilyName { get; set; }
        public string Email { get; set; }
        public string WorkPhone { get; set; }
        public string Fax { get; set; }
        public string CellPhone { get; set; }
        public string AltPhone { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public string Notes { get; set; }
        public List<CustomField> CustomFields { get; set; }
        public string DateModifiied { get; set; }
        public DateTime _DateModified { get { return Helpers.Modifiers.GetDate(this.DateModifiied); } }
    }

    public class SimpleContact : SimpleItem
    {
        public string GivenName { get; set; }
        public string FamilyName { get; set; }
    }

    public class SiteContact : SimpleContact { }
    public class CustomerContact : SimpleContact { }
}
