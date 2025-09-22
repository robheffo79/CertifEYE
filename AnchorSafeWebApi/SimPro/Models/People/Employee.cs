using System;

namespace AnchorSafe.SimPro.DTO.Models.People
{
    public class EmployeeContainer : SimpleContainer<People.EmployeeListItem> { }

    public class EmployeeListItem : SimpleItem, IEquatable<EmployeeListItem>
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }

        public bool Equals(EmployeeListItem other)
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

    public class Employee
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public Address Address { get; set; }
        public string DateOfHire { get; set; }
        public DateTime _DateOfHire { get { return Helpers.Modifiers.GetDate(this.DateOfHire); } }
        //public string DateOfBirth { get; set; }
        public Contact PrimaryContact { get; set; }
        public Contact EmergencyContact { get; set; }
        public bool Archived { get; set; }
    }
}
