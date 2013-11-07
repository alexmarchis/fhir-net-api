﻿using Hl7.Fhir.Model;
using Hl7.Fhir.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Hl7.Fhir.Serialization
{
    public class PropertyMapping
    {
        public string Name { get; private set; }        
        public Type PolymorphicBase { get; private set; }

        public bool IsPolymorhic
        {
            get { return PolymorphicBase != null; }
        }

        public bool MayRepeat { get; private set; }

        public ClassMapping MappedPropertyType { get; private set; }

        public Type EnumType { get; private set; }

        public bool IsEnumeratedType
        {
            get { return EnumType != null; }
        }

        public PropertyInfo ImplementingProperty { get; private set; }

        public static bool TryCreate(ModelInspector inspector, PropertyInfo prop, out PropertyMapping result)
        {
            result = null;

            try
            {
                result = Create(inspector, prop);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static PropertyMapping Create(ModelInspector inspector, PropertyInfo prop)
        {
            if (prop == null) throw Error.ArgumentNull("prop");
            if (inspector == null) throw Error.ArgumentNull("inspector");

            PropertyMapping result = new PropertyMapping();
            result.Name = getMappedElementName(prop);
            result.ImplementingProperty = prop;

            result.MayRepeat = ReflectionHelper.IsTypedCollection(prop.PropertyType);

            Type elementType = prop.PropertyType;

            // If this is a collection, map to the collection's element type, not the collection type
            if (result.MayRepeat)
            {
                elementType = ReflectionHelper.GetCollectionItemType(elementType);
            }

            if (elementType == typeof(Element) || elementType == typeof(Resource))
            {
                // Polymorphic types:
                // * Polymorphic (choice) properties are generated to have type Element
                // * The contents of the 'contained' property of a resource are of type Resource
                //TODO: a profiled class may have a single fixed type left as choice, so could map this to a fixed type.
                result.PolymorphicBase = elementType;  // keep the type, so we know whether to expect any element or any resource (contained)
                result.MappedPropertyType = null;   // polymorphic, so cannot be known in advance (look at member name in instance)
            }
            else if (isFhirPrimtiveMappedAsNativePrimitive(elementType))
            {
                // TODO: Handle mapping to direct primitives instead to PrimitiveElement
                // (we will not currently encounter this in the generated mappings, but it is conceivable
                // in custom mapped classes)
                throw Error.NotSupported("Property {0} on type {1}: mappings to .NET native types are not yet supported", prop.Name, prop.DeclaringType.Name);
            }
            else
            {
                // Special case: this is a member that used the closed generic Code<T> type - 
                // do mapping for its open, defining type instead
                if (elementType.IsGenericType)
                {
                    if (ReflectionHelper.IsClosedGenericType(elementType) &&  
                        ReflectionHelper.IsConstructedFromGenericTypeDefinition(elementType, typeof(Code<>)) )
                    {
                        result.EnumType = elementType.GetGenericArguments()[0];
                        elementType = elementType.GetGenericTypeDefinition();
                    }
                    else
                        throw Error.NotSupported("Property {0} on type {1} uses an open generic type, which is not yet supported", prop.Name, prop.DeclaringType.Name);
                }

                // pre-fetch the mapping for this property, saves lookups while parsing instance
                var mappedPropertyType = inspector.FindClassMappingByImplementingType(elementType);
                if (mappedPropertyType == null)
                    throw Error.InvalidOperation("Property {0} on type {1}: property maps to a type that is not recognized as a mapped FHIR type", prop.Name, prop.DeclaringType.Name);

                result.MappedPropertyType = mappedPropertyType;
            }

            return result;
        }


        public bool MatchesSuffixedName(string suffixedName)
        {
            if (suffixedName == null) throw Error.ArgumentNull("suffixedName");

            return this.IsPolymorhic &&
                       suffixedName.ToUpperInvariant().StartsWith(Name.ToUpperInvariant());
        }

        public string GetSuffixFromName(string suffixedName)
        {
            if (suffixedName == null) throw Error.ArgumentNull("suffixedName");

            if (MatchesSuffixedName(suffixedName))
                return suffixedName.Remove(0, Name.Length);
            else
                throw Error.Argument("suffixedName", "The given suffixed name {0} does not match this property's name {1}",
                                            suffixedName, Name);
        }

        private static string getMappedElementName(PropertyInfo prop)
        {
            var attr = (FhirElementAttribute)Attribute.GetCustomAttribute(prop, typeof(FhirElementAttribute));

            if (attr != null)
                return attr.Name;
            else
                return prop.Name;
        }

        private static bool isFhirPrimtiveMappedAsNativePrimitive(Type type)
        {
            if (type == typeof(bool?) ||
                   type == typeof(int?) ||
                   type == typeof(decimal?) ||
                   type == typeof(byte[]) ||
                   type == typeof(DateTimeOffset?) ||
                   type == typeof(string))
                return true;

            // Special case, allow Nullable<enum>
            if (ReflectionHelper.IsNullableType(type))
            {
                var nullable = ReflectionHelper.GetNullableArgument(type);
                if (nullable.IsEnum) return true;
            }

            return false;
        }

        internal static bool IsMappableElement(PropertyInfo prop)
        {
            if (prop == null) throw Error.ArgumentNull("prop");

            var type = prop.PropertyType;

            return type == typeof(Element)
                        || ClassMapping.IsFhirComplexType(type)
                        || ClassMapping.IsFhirPrimitive(type)
                        || isFhirPrimtiveMappedAsNativePrimitive(type);
        }
    }
}