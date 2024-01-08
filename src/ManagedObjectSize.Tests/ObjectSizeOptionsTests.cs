using System;

namespace ManagedObjectSize.Tests
{
    [TestClass]
    public class ObjectSizeOptionsTests
    {
        [DataTestMethod]
        [DataRow(0.95, 5, 100, 80)]
        [DataRow(0.99, 5, 100, 87)]
        [DataRow(0.95, 5, 100_000_000, 384)]
        [DataRow(0.99, 5, 100_000_000, 663)]
        public void CalculateSampleCount(double confidenceLevel, int confidenceInterval, int populationSize, int expectedSampleSize)
        {
            int actualSampleSize = Utils.CalculateSampleCount(confidenceLevel, confidenceInterval, populationSize);
            Assert.AreEqual(expectedSampleSize, actualSampleSize);
        }
    }
}
