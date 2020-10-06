using System;

namespace Dapper.SimpleWrapper.Common
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public abstract class FilterAttributeBase : Attribute
    {
        public string FieldName { get; }

        protected FilterAttributeBase(string fieldName = null)
        {
            FieldName = fieldName;
        }
    }

    /// <summary>
    /// Instructs query handlers to allow sorting on this property or field
    /// </summary>
    public class SortableAttribute : FilterAttributeBase
    {
        public SortableAttribute(string fieldName = null) : base(fieldName) { }
    }

    /// <summary>
    /// Instructs query handlers to allow search on this property or field
    /// </summary>
    public class SearchableAttribute : FilterAttributeBase
    {
        public readonly int MinimumLength;

        public SearchableAttribute(string fieldName = null, int minimumLength = 3) : base(fieldName)
        {
            MinimumLength = minimumLength;
        }
    }
}
