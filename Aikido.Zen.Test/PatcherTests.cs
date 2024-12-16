using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.Test
{
    public class PatcherTests
    {
        [Test]
        public void Patch_ShouldApplyPatches()
        {
            // Arrange
            Patcher.Unpatch(); // Ensure clean state

            // Act
            Patcher.Patch();

            // Assert
            Assert.That(Harmony.HasAnyPatches("aikido.zen"), Is.True);
        }

        [Test]
        public void Unpatch_ShouldRemovePatches()
        {
            // Arrange
            Patcher.Patch(); // Ensure patches are applied

            // Act
            Patcher.Unpatch();

            // Assert
            Assert.That(Harmony.HasAnyPatches("aikido.zen"), Is.False);
        }

        [TearDown]
        public void Cleanup()
        {
            Patcher.Unpatch();
        }
    }
}
