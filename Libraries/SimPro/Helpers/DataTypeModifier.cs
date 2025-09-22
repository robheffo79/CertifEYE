using System;
using System.Linq;
using System.Reflection;

namespace AnchorSafe.SimPro.Helpers
{
    public static class Modifiers
    {
        public static DateTime GetDate(string date)
        {
            if (!String.IsNullOrEmpty(date))
            {
                return DateTime.Parse(date);
            }
            else
            {
                return DateTime.MinValue;
            }
        }

        public static String ParseTime(string time)
        {
            if (!time.Contains(':') || time.Length != 5)
            {
                throw new Exception("ERROR: ParseTime: Invalid time");
            }

            return time;
        }
    }

    public static class EnumExtensions
    {
        public static string GetDescription(this Enum GenericEnum)
        {
            Type genericEnumType = GenericEnum.GetType();
            MemberInfo[] memberInfo = genericEnumType.GetMember(GenericEnum.ToString());
            if ((memberInfo != null && memberInfo.Length > 0))
            {
                object[] attributes = memberInfo[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                if ((attributes != null && attributes.Length > 0))
                {
                    return ((System.ComponentModel.DescriptionAttribute)attributes.ElementAt(0)).Description;
                }
            }
            return GenericEnum.ToString();
        }
    }
}
