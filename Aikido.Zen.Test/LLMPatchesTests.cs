using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Sinks;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class LLMPatchesTests
    {
        private Context _context = null!;

        [SetUp]
        public void SetUp()
        {
            Patcher.Unpatch();
            _context = new Context();
            Patcher.PatchSinks(() => _context);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
        }

        [Test]
        public void PatchMethod_ForwardsCompletedCallToSink()
        {
            Assert.DoesNotThrow(() => LLMPatches.LLMCallCompleted(
                null!,
                null!,
                GetMethod(typeof(object), nameof(ToString))));
        }

        private static MethodInfo GetMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            return method;
        }
    }
}
