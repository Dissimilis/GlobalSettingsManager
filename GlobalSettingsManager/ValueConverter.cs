using System;
using System.ComponentModel;
using System.Globalization;

namespace GlobalSettingsManager
{

    /// <summary>
    /// Custom datetime converted because invariant culture's date is in english format!
    /// </summary>
    public class IsoDateTimeTypeConverter : TypeConverter
    {
        private const string Pattern = "yyyy-MM-dd HH-mm-ss";
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
                return DateTime.ParseExact(value.ToString(), Pattern, culture);
            return base.ConvertFrom(context, culture, value);
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is DateTime)
            {
                return ((DateTime)value).ToString(Pattern);
            }
            return base.ConvertFrom(context, culture, value);
        }
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertFrom(context, destinationType);
        }


    }

    public class ValueConverter
    {

        /// <summary>
        /// Complex types serializer; Default is Xml serializer
        /// </summary>
        public Func<object, string> Serialize { get; set; }
        /// <summary>
        /// Complex types deserializer; Default is Xml serializer
        /// </summary>
        public Func<string, Type, object> Deserialize { get; set; }

        public ValueConverter()
        {
            Serialize = DefaultXmlSerializer.Serialize;
            Deserialize = DefaultXmlSerializer.Deserialize;
        }

        public virtual string ConvertToString(object value)
        {
            if (value == null)
                return null;

            if (value is string)
                return value.ToString();

            var converter = GetConverter(value.GetType());
            if (converter.CanConvertFrom(typeof(string)) && converter.CanConvertTo(typeof(string)))
            {
                return converter.ConvertToString(value);
            }
            else
            {
                return Serialize(value);
            }

        }

        public virtual object ConvertFromString(string value, Type type)
        {
            if (type == typeof (string))
                return value;
            var converter = GetConverter(type);
            if (converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromString(value);
            }
            else
            {
                return Deserialize(value, type);
            }
        }

        private TypeConverter GetConverter(Type type)
        {
            if (type == typeof(DateTime))
                return new IsoDateTimeTypeConverter();
            return TypeDescriptor.GetConverter(type);
        }
    }
}
