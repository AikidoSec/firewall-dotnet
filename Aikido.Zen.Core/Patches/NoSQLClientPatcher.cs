using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Vulnerabilities;
using System.Reflection;
using System.Text.Json;

namespace Aikido.Zen.Core.Patches
{
    public static class NoSQLClientPatcher
    {
        // Cache the PropertyInfo for the Document property
        private static PropertyInfo documentPropertyInfo;

        public static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance, string assembly, Context context)
        {
            if (context == null)
            {
                return true;
            }

            var command = __args[2] as JsonElement?;
            if (command == null)
            {
                // Check if the PropertyInfo is already cached
                if (documentPropertyInfo == null)
                {
                    documentPropertyInfo = __args[2].GetType().GetProperty("Document");
                }

                if (documentPropertyInfo != null)
                {
                    var bsonDocument = documentPropertyInfo.GetValue(__args[2]);
                    command = ConvertBsonToJson(bsonDocument.ToString());
                }
            }
            if (command.HasValue && NoSQLInjectionDetector.DetectNoSQLInjection(context, command.Value))
            {
                // keep going if dry mode
                if (EnvironmentHelper.DryMode)
                {
                    return true;
                }
                throw AikidoException.NoSQLInjectionDetected();
            }
            return true;
        }

        // Method to convert BSON byte array to JSON string
        private static JsonElement ConvertBsonToJson(string bsonString)
        {
            return JsonSerializer.Deserialize<JsonElement>(bsonString);
        }
    }
}
