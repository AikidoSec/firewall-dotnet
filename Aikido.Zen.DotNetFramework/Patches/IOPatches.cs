using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.AccessControl;
using System.Web;

namespace Aikido.Zen.DotNetFramework.Patches
{
    internal static class IOPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // File operations
            PatchMethod(harmony, typeof(File), "Open", new[] { typeof(string), typeof(FileMode) });
            PatchMethod(harmony, typeof(File), "OpenRead");
            PatchMethod(harmony, typeof(File), "OpenWrite");
            PatchMethod(harmony, typeof(File), "Create", new[] { typeof(string), typeof(int), typeof(FileOptions), typeof(FileSystemSecurity) });
            PatchMethod(harmony, typeof(File), "Delete");
            PatchMethod(harmony, typeof(File), "Copy", new[] { typeof(string), typeof(string), typeof(bool) });
            PatchMethod(harmony, typeof(File), "Move", new[] { typeof(string), typeof(string), typeof(bool) });
            PatchMethod(harmony, typeof(File), "ReadAllText", new[] { typeof(string) });
            PatchMethod(harmony, typeof(File), "ReadAllBytes", new[] { typeof(string) });
            PatchMethod(harmony, typeof(File), "WriteAllText", new[] { typeof(string), typeof(string) });
            PatchMethod(harmony, typeof(File), "WriteAllBytes", new[] { typeof(string), typeof(byte[]) });
            PatchMethod(harmony, typeof(File), "AppendAllText", new[] { typeof(string), typeof(string) });

            // Directory operations
            PatchMethod(harmony, typeof(Directory), "CreateDirectory", new[] { typeof(string), typeof(DirectorySecurity) });
            PatchMethod(harmony, typeof(Directory), "Delete", new[] { typeof(string), typeof(bool) });
            PatchMethod(harmony, typeof(Directory), "GetFiles", new[] { typeof(string)  });
            PatchMethod(harmony, typeof(Directory), "GetFiles", new[] { typeof(string), typeof(string) });
            PatchMethod(harmony, typeof(Directory), "GetFiles", new[] { typeof(string), typeof(string), typeof(SearchOption) });
            PatchMethod(harmony, typeof(Directory), "GetDirectories", new[] { typeof(string) });
            PatchMethod(harmony, typeof(Directory), "GetDirectories", new[] { typeof(string), typeof(string) });
            PatchMethod(harmony, typeof(Directory), "GetDirectories", new[] { typeof(string), typeof(string), typeof(SearchOption) });
        }

        private static void PatchMethod(Harmony harmony, Type type, string methodName, Type[] parameters = null)
        {
            try
            {
                var method = parameters == null ?
                    AccessTools.Method(type, methodName) :
                    AccessTools.Method(type, methodName, parameters);

                if (method != null)
                {
                    harmony.Patch(method,
                        new HarmonyMethod(typeof(IOPatches).GetMethod(nameof(OnFileOperation),
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
                }

            }
            catch (Exception e)
            {

                throw;
            }
        }

        private static bool OnFileOperation(object[] __args, System.Reflection.MethodBase __originalMethod)
        {
            var assembly = __originalMethod.DeclaringType.Assembly.FullName?.Split(new[] { ", Culture=" }, StringSplitOptions.None)[0];
            var context = Zen.GetContext();

            return PathTraversalHelper.DetectPathTraversal(__args, assembly, context, __originalMethod.Name);
        }
    }
}
