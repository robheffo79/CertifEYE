using System;
using System.Collections.Generic;

namespace AnchorSafe.SimPro.DTO.Models
{
    public class SimpleContainer<T>
    {
        public DateTime LastUpdated { get; set; }
        public List<T> Items { get; set; }
    }

    public class SimpleItem : IEquatable<SimpleItem>//, IComparable<SimpleItem>, IComparable
    {
        public int ID { get; set; }

        public bool Equals(SimpleItem other)
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

        /*public int CompareTo(SimpleItem other)
        {
            if (null == other) { return 1; }
            return ID.CompareTo(other.ID);
        }

        public int CompareTo(object obj)
        {
            if (null == obj) { return 1; }
            var other = obj as SimpleItem;
            if (ID == other.ID) { return 0; }
            return (ID > other.ID) ? 1 : -1;
        }*/
    }


    public class CustomFieldItem
    {
        public CustomField CustomField { get; set; }
        public string Value { get; set; }
    }

    public class CustomField
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string[] ListItems { get; set; }
        public bool IsMandatory { get; set; }
    }

    public class Status
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }

    public class Total
    {
        public double ExTax { get; set; }
        public double Tax { get; set; }
        public double IncTax { get; set; }
    }

    public class Totals
    {
        /* TO DO ? */
    }

    public class ResponseTime
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int Days { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
    }

    public class STC
    {
        public bool STCsEligible { get; set; }
        public bool VEECsEligible { get; set; }
        public double STCValue { get; set; }
        public double VEECValue { get; set; }
    }
}
