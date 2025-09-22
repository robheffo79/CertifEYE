using System;
using System.Collections.Generic;

namespace AnchorSafe.SimPro.DTO.Models.People
{
    public class CustomerContainer : SimpleContainer<People.CustomerListItem> { }

    public class CustomerListItem : SimpleCustomer, IEquatable<CustomerListItem>
    {
        public bool Equals(CustomerListItem other)
        {
            if (Object.ReferenceEquals(this, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }
        public override int GetHashCode()
        {
            //Get hash code for the ID field.
            int hashID = ID.GetHashCode();

            //Calculate the hash code for the item.
            return hashID;
        }
    }

    public class Customer : SimpleItem
    {
        public string GivenName { get; set; }
        public string FamilyName { get; set; }
        public string CustomerType { get; set; }
        public Address Address { get; set; }
        public List<SimpleSite> Sites { get; set; }
        public bool Archived { get; set; }

    }

    public class SimpleCustomer : SimpleContact
    {
        public string CompanyName { get; set; }
    }
}
