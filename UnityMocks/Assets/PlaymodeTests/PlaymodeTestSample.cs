using System.Collections;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class StaticClassA
    {
        public static int GetValue(int a)
        {
            return a;
        }
    }
    public class PlaymodeTestSample
    {
        // A Test behaves as an ordinary method
        [Test]
        public void PlaymodeTestSampleSimplePasses()
        {
            using (NSubstitute.Elevated.SubstituteStatic.For<StaticClassA>())
            {
                StaticClassA.GetValue(4).Returns(2);
                Assert.That(StaticClassA.GetValue(4), Is.EqualTo(2));
            }
            // Use the Assert class to test conditions
        }
    }
}
