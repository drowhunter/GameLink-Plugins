using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SharedLib.TelemetryHelper.Convertors
{
    /// <summary>
    /// Converts a serialized incomplete dictionary by updating the values over time
    /// </summary>
    internal class SparseJsonDictConverter : IByteConverter<Dictionary<string, object>>
    {
        public Dictionary<string, object> Data = new();
        private readonly JsonSerializerSettings _options;

        public SparseJsonDictConverter() : this(new JsonSerializerSettings())
        {
        }
        public SparseJsonDictConverter(JsonSerializerSettings options)
        {
            _options = new JsonSerializerSettings(options);
        }

        public void Reset()
        {
            Data = new Dictionary<string, object>();
        }

        public void SetData(Dictionary<string, object> data)
        {
            Data = data;
        }

        public byte[] ToBytes(Dictionary<string, object> data)
        {
            string json = JsonConvert.SerializeObject(data, _options);


            return Encoding.UTF8.GetBytes(json);
        }
        public Dictionary<string, object> FromBytes(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);

            using var sr = new StringReader(json);
            using var reader = new JsonTextReader(sr);
            var serializer = JsonSerializer.Create(_options);

            // If root is not an object, nothing to merge
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                return Data;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var propName = (string)reader.Value!;

                    // Advance to the value token
                    if (!reader.Read())
                        break;

                    // Deserialize the value at the current reader position (consumes nested tokens)
                    var value = serializer.Deserialize(reader);
                    Data[propName] = value!;
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }
            }

            return Data;
        }
    }
}
