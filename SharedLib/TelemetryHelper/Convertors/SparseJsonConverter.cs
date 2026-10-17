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
    internal class SparseJsonConverter<T> : IByteConverter<T> where T : class, new()
    {
        public T Data = new T();
        private readonly JsonSerializerSettings _options;

        public SparseJsonConverter() : this(new JsonSerializerSettings())
        {
        }
        public SparseJsonConverter(JsonSerializerSettings options)
        {
            _options = new JsonSerializerSettings(options);
        }


        public byte[] ToBytes(T data)
        {
            string json = JsonConvert.SerializeObject(data, _options);


            return Encoding.UTF8.GetBytes(json);
        }

        public T FromBytes(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);

            using var sr = new StringReader(json);
            using var reader = new JsonTextReader(sr);
            var serializer = JsonSerializer.Create(_options);

            // If root is not an object, nothing to merge
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                return Data;

            var tType = typeof(T);

            // Detect IDictionary<string, object> implementations
            bool isStringObjectDict = typeof(IDictionary<string, object>).IsAssignableFrom(tType);
            if (!isStringObjectDict)
            {
                foreach (var iface in tType.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    {
                        var args = iface.GetGenericArguments();
                        if (args.Length == 2 && args[0] == typeof(string) && args[1] == typeof(object))
                        {
                            isStringObjectDict = true;
                            break;
                        }
                    }
                }
            }

            if (isStringObjectDict)
            {
                // Ensure Data is a dictionary we can update
                IDictionary<string, object>? dict = Data as IDictionary<string, object>;
                if (dict == null)
                {
                    // Try to create an instance of T and see if it implements the dictionary
                    try
                    {
                        var created = Activator.CreateInstance<T>();
                        if (created is IDictionary<string, object> createdDict)
                        {
                            Data = created!;
                            dict = createdDict;
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    if (dict == null)
                    {
                        // If T can accept a Dictionary<string, object>, create that
                        if (tType.IsAssignableFrom(typeof(Dictionary<string, object>)) || typeof(IDictionary<string, object>).IsAssignableFrom(tType))
                        {
                            var newDict = new Dictionary<string, object>();
                            Data = (T)(object)newDict!;
                            dict = newDict;
                        }
                        else
                        {
                            // Unable to instantiate a compatible dictionary type; fall back to deserializing whole object
                            var whole = serializer.Deserialize<T>(reader);
                            if (whole != null)
                                Data = whole;
                            return Data;
                        }
                    }
                }

                // Read properties and update the dictionary as we go
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName)
                    {
                        var propName = (string)reader.Value!;

                        // Advance to the value token
                        if (!reader.Read())
                            break;

                        var value = serializer.Deserialize(reader);
                        dict[propName] = value!;
                    }
                    else if (reader.TokenType == JsonToken.EndObject)
                    {
                        break;
                    }
                }

                return Data;
            }

            // Non-dictionary case: if Data is default, deserialize; otherwise populate (merge) into existing Data
            if (EqualityComparer<T>.Default.Equals(Data, default(T)!))
            {
                // Deserialize the entire object from the current reader position
                var result = serializer.Deserialize<T>(reader);
                if (result != null)
                    Data = result;
            }
            else
            {
                // Populate will merge into the existing object
                serializer.Populate(reader, Data!);
            }

            return Data;
        }
    }
}
