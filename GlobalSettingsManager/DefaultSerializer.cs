using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace GlobalSettingsManager
{
    public static class DefaultXmlSerializer
    {
        public static string Serialize(object obj)
        {
            var serializer = new XmlSerializer(obj.GetType());
            var result = new StringBuilder();
            using (var writer = new StringWriter(result))
            {
                serializer.Serialize(writer, obj);
                return result.ToString();
            }
        }

        public static object Deserialize(string obj, Type type)
        {
            var serializer = new XmlSerializer(type);
            using (var reader = new StringReader(obj))
            {
                var result = serializer.Deserialize(reader);
                return result;
            }
        }
    }
}
