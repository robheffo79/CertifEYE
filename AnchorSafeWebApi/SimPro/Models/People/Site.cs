using AnchorSafe.SimPro.DTO.Models.Asset;
using System;
using System.Collections.Generic;

namespace AnchorSafe.SimPro.DTO.Models.People
{
    public class SiteContainer : SimpleContainer<People.SiteListItem> { }

    public class SiteListItem : SimpleItem, IEquatable<SiteListItem>
    {
        public string Name { get; set; }
        public Address Address { get; set; }
        public List<SimpleCustomer> Customers { get; set; }
        public bool Archived { get; set; }

        public bool Equals(SiteListItem other)
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

    public class Site
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public Address Address { get; set; }
        public BillingAddress BillingAddress { get; set; }
        public string BillingContact { get; set; }
        public Contact PrimaryContact { get; set; }
        public string PublicNotes { get; set; }
        public string PrivateNotes { get; set; }
        public Zone Zone { get; set; }
        public List<PreferredTechnician> PreferredTechnicians { get; set; }
        public string DateModified { get; set; }
        public DateTime _DateModified { get { return Helpers.Modifiers.GetDate(this.DateModified); } }
        public List<SimpleCustomer> Customers { get; set; }
        public bool Archived { get; set; }
        public int? STCZone { get; set; }
        public string VEECZone { get; set; }
        public List<CustomField> CustomFields { get; set; }

    }

    public class SimpleSite
    {
        public int ID { get; set; }
        public string Name { get; set; }

    }


    public class Zone
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }

    public class PreferredTechnician
    {
        public People.StaffMember Staff { get; set; }
        public AssetType AssetType { get; set; }
        public ServiceLevel ServiceLevel { get; set; }

    }

    public class ServiceLevel
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }
}
