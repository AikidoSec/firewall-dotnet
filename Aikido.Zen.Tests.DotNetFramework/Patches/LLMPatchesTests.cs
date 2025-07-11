using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.DotNetFramework.Patches;
using HarmonyLib;
using NUnit.Framework;
using OpenAI.Chat;

namespace Aikido.Zen.Tests.DotNetFramework.Patches
{
    [TestFixture]
    public class LLMPatchesTests
    {
        private Harmony _harmony;
        private static readonly string HarmonyId = "com.aikido.zen.tests.dotnetframework.llmpatches";

        [SetUp]
        public void OneTimeSetUp()
        {
            _harmony = new Harmony(HarmonyId);
            LLMPatches.ApplyPatches(_harmony);

            // Force OpenAI assembly to be loaded
            try
            {
                // This will cause the OpenAI assembly to be loaded into the AppDomain
                var _ = typeof(ChatClient); // Updated to use the correct namespace and type
            }
            catch
            {
                // Ignore if OpenAI types aren't available
            }
        }

        [TearDown]
        public void OneTimeTearDown()
        {
            _harmony.UnpatchAll(HarmonyId);
        }

        [Test]
        public void OpenAI_ChatClient_CompleteChat_IsPatched()
        {
            var method = ReflectionHelper.GetMethodFromAssembly("OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat",
                "System.Collections.Generic.IEnumerable`1[OpenAI.Chat.ChatMessage]",
                "OpenAI.Chat.ChatCompletionOptions",
                "System.Threading.CancellationToken");

            if (method == null)
            {
                Assert.Inconclusive("OpenAI package not available or CompleteChat method not found");
                return;
            }

            var patches = Harmony.GetPatchInfo(method);
            Assert.That(patches, Is.Not.Null, "Harmony patches should exist for OpenAI CompleteChat.");
            Assert.That(patches.Postfixes.Any(p => p.owner == HarmonyId), Is.True, "Our postfix should be applied to OpenAI CompleteChat.");
        }

        [Test]
        public void OpenAI_ChatClient_CompleteChatAsync_IsPatched()
        {
            var method = ReflectionHelper.GetMethodFromAssembly("OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync",
                "System.Collections.Generic.IEnumerable`1[OpenAI.Chat.ChatMessage]",
                "OpenAI.Chat.ChatCompletionOptions",
                "System.Threading.CancellationToken");

            if (method == null)
            {
                Assert.Inconclusive("OpenAI package not available or CompleteChatAsync method not found");
                return;
            }

            var patches = Harmony.GetPatchInfo(method);
            Assert.That(patches, Is.Not.Null, "Harmony patches should exist for OpenAI CompleteChatAsync.");
            Assert.That(patches.Postfixes.Any(p => p.owner == HarmonyId), Is.True, "Our postfix should be applied to OpenAI CompleteChatAsync.");
        }

        [Test]
        public void LLMPatches_ApplyPatches_DoesNotThrow()
        {
            // Test that applying patches doesn't throw exceptions even if some assemblies are missing
            var testHarmony = new Harmony("test.harmony.llm");
            Assert.DoesNotThrow(() => LLMPatches.ApplyPatches(testHarmony));
            testHarmony.UnpatchAll("test.harmony.llm");
        }
    }
}
