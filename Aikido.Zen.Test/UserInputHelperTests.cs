using Aikido.Zen.Core.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class UserInputHelperTests
    {
        [TestCase("query.url", ".url")]
        [TestCase("query.url|decoded", ".url")]
        [TestCase("body.image.url", ".image.url")]
        [TestCase("body.items.0.url", ".items.[0].url")]
        [TestCase("headers.Authorization", ".Authorization")]
        [TestCase("body", ".")]
        public void GetAttackPathFromUserInputKey_ReturnsExpectedPath(string input, string expected)
        {
            Assert.That(UserInputHelper.GetAttackPathFromUserInputKey(input), Is.EqualTo(expected));
        }
    }
}
