using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetFramework.Patches
{
    /// <summary>
    /// Applies patches to System.IO methods for monitoring file and directory operations.
    /// </summary>
    internal static class IOPatches
    {
        // Thread-local flag to prevent re-entrancy during assembly loading
        private static readonly ThreadLocal<bool> _isProcessing = new ThreadLocal<bool>(() => false);
        /// <summary>
        /// Applies all IO patches using the provided Harmony instance.
        /// </summary>
        /// <param name="harmony">The Harmony instance to use for patching.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            // File operations
            Patch(harmony, typeof(File).GetMethod("Open", new[] { typeof(string), typeof(FileMode) }), nameof(PrefixFileOpen));
            Patch(harmony, AccessTools.Method(typeof(File), "OpenRead"), nameof(PrefixFileOpenRead));
            Patch(harmony, AccessTools.Method(typeof(File), "OpenWrite"), nameof(PrefixFileOpenWrite));
            Patch(harmony, typeof(File).GetMethod("Create", new[] { typeof(string), typeof(int), typeof(FileOptions) }), nameof(PrefixFileCreate));
            Patch(harmony, AccessTools.Method(typeof(File), "Delete"), nameof(PrefixFileDelete));
            Patch(harmony, typeof(File).GetMethod("Copy", new[] { typeof(string), typeof(string), typeof(bool) }), nameof(PrefixFileCopy));
            Patch(harmony, typeof(File).GetMethod("Move", new[] { typeof(string), typeof(string) }), nameof(PrefixFileMove));
            Patch(harmony, AccessTools.Method(typeof(File), "ReadAllText", new[] { typeof(string) }), nameof(PrefixFileReadAllText));
            Patch(harmony, AccessTools.Method(typeof(File), "ReadAllBytes", new[] { typeof(string) }), nameof(PrefixFileReadAllBytes));
            Patch(harmony, typeof(File).GetMethod("WriteAllText", new[] { typeof(string), typeof(string) }), nameof(PrefixFileWriteAllText));
            Patch(harmony, typeof(File).GetMethod("WriteAllBytes", new[] { typeof(string), typeof(byte[]) }), nameof(PrefixFileWriteAllBytes));
            Patch(harmony, typeof(File).GetMethod("AppendAllText", new[] { typeof(string), typeof(string) }), nameof(PrefixFileAppendAllText));

            // Directory operations
            Patch(harmony, typeof(Directory).GetMethod("CreateDirectory", new[] { typeof(string), typeof(DirectorySecurity) }), nameof(PrefixDirectoryCreateDirectory));
            Patch(harmony, typeof(Directory).GetMethod("Delete", new[] { typeof(string), typeof(bool) }), nameof(PrefixDirectoryDelete));
            Patch(harmony, typeof(Directory).GetMethod("GetFiles", new[] { typeof(string) }), nameof(PrefixDirectoryGetFiles));
            Patch(harmony, typeof(Directory).GetMethod("GetFiles", new[] { typeof(string), typeof(string) }), nameof(PrefixDirectoryGetFilesWithPattern));
            Patch(harmony, typeof(Directory).GetMethod("GetFiles", new[] { typeof(string), typeof(string), typeof(SearchOption) }), nameof(PrefixDirectoryGetFilesWithPatternAndOption));
            Patch(harmony, typeof(Directory).GetMethod("GetDirectories", new[] { typeof(string) }), nameof(PrefixDirectoryGetDirectories));
            Patch(harmony, typeof(Directory).GetMethod("GetDirectories", new[] { typeof(string), typeof(string) }), nameof(PrefixDirectoryGetDirectoriesWithPattern));
            Patch(harmony, typeof(Directory).GetMethod("GetDirectories", new[] { typeof(string), typeof(string), typeof(SearchOption) }), nameof(PrefixDirectoryGetDirectoriesWithPatternAndOption));
        }

        private static void Patch(Harmony harmony, MethodBase method, string prefixMethodName)
        {
            if (method == null) return;
            var prefix = typeof(IOPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(method, new HarmonyMethod(prefix));
        }

        #region File Operation Prefixes
        private static bool PrefixFileOpen(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileOpenRead(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileOpenWrite(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileCreate(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileDelete(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileReadAllText(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileReadAllBytes(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileCopy(string sourceFileName, string destFileName, MethodBase __originalMethod) => OnFileOperation(new[] { sourceFileName, destFileName }, __originalMethod);
        private static bool PrefixFileMove(string sourceFileName, string destFileName, MethodBase __originalMethod) => OnFileOperation(new[] { sourceFileName, destFileName }, __originalMethod);
        private static bool PrefixFileWriteAllText(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileWriteAllBytes(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixFileAppendAllText(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        #endregion

        #region Directory Operation Prefixes
        private static bool PrefixDirectoryCreateDirectory(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixDirectoryDelete(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixDirectoryGetFiles(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixDirectoryGetFilesWithPattern(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixDirectoryGetFilesWithPatternAndOption(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixDirectoryGetDirectories(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixDirectoryGetDirectoriesWithPattern(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        private static bool PrefixDirectoryGetDirectoriesWithPatternAndOption(string path, MethodBase __originalMethod) => OnFileOperation(new[] { path }, __originalMethod);
        #endregion

        private static bool OnFileOperation(string[] paths, MethodBase originalMethod)
        {
            // Prevent re-entrancy that can occur during Costura assembly loading
            if (_isProcessing.Value)
            {
                return true; // Continue with original method execution
            }



            try
            {
                _isProcessing.Value = true;
                var context = Zen.GetContext();

                return IOPatcher.OnFileOperation(paths, originalMethod, context);
            }
            finally
            {
                _isProcessing.Value = false;
            }
        }


    }
}
