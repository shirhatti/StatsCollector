using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace StatsApi
{
    public class Package : IEquatable<Package>
    {
        public string PackageName { get; set; }
        public string Version { get; set; }
        public DateTime LastUpdated { get; set; }

        public bool Equals(Package other)
        {
            return (GetHashCode() == other.GetHashCode());
        }

        public override int GetHashCode()
        {
            return PackageName.GetHashCode() + Version.GetHashCode();
        }
    }

    public class PackageDictionaryJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(Dictionary<Package, int>));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach(KeyValuePair<Package, int> kvpair in (IDictionary<Package, int>)value)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(kvpair.Key.PackageName));
                writer.WriteValue(kvpair.Key.PackageName);
                writer.WritePropertyName(nameof(kvpair.Key.Version));
                writer.WriteValue(kvpair.Key.Version);
                writer.WritePropertyName(nameof(kvpair.Value));
                writer.WriteValue(kvpair.Value);
                writer.WritePropertyName(nameof(kvpair.Key.LastUpdated));
                writer.WriteValue(kvpair.Key.LastUpdated);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }
}