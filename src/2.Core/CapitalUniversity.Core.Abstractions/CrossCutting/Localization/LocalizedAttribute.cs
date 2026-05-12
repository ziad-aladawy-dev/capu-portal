using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Localization
{
    /// <summary>
    /// Attribute to specify localized strings for a property or field.
    /// Used for enums
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedAttribute : Attribute
    {
        public string Ar { get; }
        public string En { get; }

        public LocalizedAttribute(string ar, string en)
        {
            Ar = ar;
            En = en;
        }
    }
}