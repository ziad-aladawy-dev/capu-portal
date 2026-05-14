using System;

<<<<<<<< Updated upstream:src/2.Core/CapitalUniversity.Core.Domain/Common/LocalizedAttribute.cs
namespace CapitalUniversity.Core.Domain.Common
========
namespace CapitalUniversity.Core.Domain.Localization
>>>>>>>> Stashed changes:src/2.Core/CapitalUniversity.Core.Domain/Localization/LocalizedAttribute.cs
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