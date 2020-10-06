using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dapper.SimpleWrapper.Common.Utilities
{
    public static class TypeInfoExtractor
    {
        /// <summary>
        /// Retrieves property information of a type whose properties have the specified attribute
        /// </summary>
        /// <typeparam name="TSubject">The subject class type to search the properties of</typeparam>
        /// <typeparam name="TAttribute">The <see cref="Attribute"/> to check the properties for</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="PropertyInfo"/></returns>
        public static IEnumerable<PropertyInfo> GetPropertiesByAttribute<TSubject, TAttribute>() where TSubject : class
        {
            TypeInfo objectTypeInfo = typeof(TSubject).GetTypeInfo();
            TypeInfo attributesTypeInfo = typeof(TAttribute).GetTypeInfo();

            var attributedProperties = objectTypeInfo.GetProperties()
                .Where(x => CustomAttributeExtensions.GetCustomAttributes((MemberInfo)x).Any(a => a.GetType().Name == attributesTypeInfo.Name));

            return attributedProperties;
        }

        /// <summary>
        /// Retrieves property names of a type whose properties have the specified attribute
        /// </summary>
        /// <typeparam name="TSubject">The subject class type to search the properties of</typeparam>
        /// <typeparam name="TAttribute">The <see cref="Attribute"/> to check the properties for</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> of property names</returns>
        public static IEnumerable<string> GetPropertyNamesByAttribute<TSubject, TAttribute>() where TSubject : class
        {
            var attributedProperties = GetPropertiesByAttribute<TSubject, TAttribute>();
            return attributedProperties.Select(x => x.Name).ToList();
        }

        /// <summary>
        /// Retrieves field information of a type whose base type is the one specified
        /// </summary>
        /// <typeparam name="TSubject">The subject class type to search the fields of</typeparam>
        /// <typeparam name="TBase">The class to check the fields for as base class</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="FieldInfo"/></returns>
        public static IEnumerable<FieldInfo> GetFieldsOfBaseType<TSubject, TBase>()
        {
            TypeInfo objectTypeInfo = typeof(TSubject).GetTypeInfo();

            var parameterFields = objectTypeInfo.GetFields()
                .Where(x => x.FieldType.BaseType == typeof(TBase));

            return parameterFields;
        }
    }
}
