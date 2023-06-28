using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft;

namespace Server.LegacyJsonSupport
{
   public class LogToAdminJsonConverter : JsonConverter<string>
   {
      public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
      {
         //JToken t = JToken.Load(reader);
         if (reader.TokenType != JsonToken.String)
            return null;
         //JObject j = JObject.Load(reader);
         //string? k = reader.ReadAsString();
         //if (k == "LogChannel" || k == "AdminChannel")
         return hasExistingValue ? existingValue : (string?)reader.Value;
         //else
         //    return null;
      }

      public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
      {
         //writer.
         //writer.WritePropertyName("AdminChannel");
         writer.WriteValue(value);
      }
   }
}
