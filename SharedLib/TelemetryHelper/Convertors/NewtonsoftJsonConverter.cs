using Newtonsoft.Json;
using System.Text;

namespace SharedLib.TelemetryHelper
{
    internal class NewtonsoftJsonConverter<T> : IByteConverter<T>
    {
        private readonly JsonSerializerSettings _options;

        public NewtonsoftJsonConverter() : this(new JsonSerializerSettings() )
        {
        }
        public NewtonsoftJsonConverter(JsonSerializerSettings options)
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
            var s = JsonConvert.DeserializeObject<T>(json, _options)!;
            return s;
        }
    }

}
