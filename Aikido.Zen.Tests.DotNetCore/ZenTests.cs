using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class ZenTests
    {
        [Test]
        public void SetRateLimitGroup_SetsGroupOnHttpContext()
        {
            var context = new DefaultHttpContext();

            Aikido.Zen.DotNetCore.Zen.SetRateLimitGroup("team-1", context);

            Assert.That(context.Items["Aikido.Zen.RateLimitGroup"], Is.EqualTo("team-1"));
        }

        [Test]
        public void SetRateLimitGroup_DoesNotSetEmptyGroup()
        {
            var context = new DefaultHttpContext();

            Aikido.Zen.DotNetCore.Zen.SetRateLimitGroup(string.Empty, context);

            Assert.That(context.Items.ContainsKey("Aikido.Zen.RateLimitGroup"), Is.False);
        }
    }
}
