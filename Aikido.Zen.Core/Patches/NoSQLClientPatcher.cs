using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using System.Reflection;
using System.Text.Json;

namespace Aikido.Zen.Core.Patches
{
    public static class NoSQLClientPatcher
    {
        public static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance, string assembly, Context context)
        {
            if (context == null)
            {
                return true;
            }

            var command = __args[0] as JsonElement?;
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
    }
}
