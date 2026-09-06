using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public sealed class GaussianSplatTests
    {
        [Test]
        public void ParserReadsAsciiPlyAndConvertsToCanonicalZ()
        {
            var ply = "ply\nformat ascii 1.0\nelement vertex 2\nproperty float x\nproperty float y\nproperty float z\nproperty uchar red\nproperty uchar green\nproperty uchar blue\nend_header\n1 2 3 255 0 0\n4 5 6 0 255 0\n";
            var points = GaussianSplatPlyParser.Parse(ply, 10);
            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(1f, points[0].position.x);
            Assert.AreEqual(-3f, points[0].position.z);
            Assert.AreEqual(255, points[0].color.r);
            Assert.AreEqual(255, points[1].color.g);
        }

        [Test]
        public void ParserRejectsBinaryPly()
        {
            var ply = "ply\nformat binary_little_endian 1.0\nelement vertex 1\nproperty float x\nproperty float y\nproperty float z\nend_header\n";
            Assert.AreEqual(0, GaussianSplatPlyParser.Parse(ply).Count);
        }

        [Test]
        public void ParserCapsSplatCount()
        {
            var ply = "ply\nformat ascii 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\nend_header\n0 0 0\n1 1 1\n2 2 2\n";
            Assert.AreEqual(2, GaussianSplatPlyParser.Parse(ply, 2).Count);
        }

        [Test]
        public void RegistryAdvertisesPocLimitsInsteadOfFull3dgsClaim()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            var capability = System.Array.Find(registry.All(), item => item.name == "media.gaussian-splat.render");
            Assert.NotNull(capability);
            Assert.IsTrue(capability.state.available);
            Assert.IsTrue(capability.state.authorized);
            Assert.IsFalse(capability.state.active);
            CollectionAssert.Contains(capability.features, "ascii-ply-poc");
            CollectionAssert.Contains(capability.features, "isotropic");
        }
    }
}
