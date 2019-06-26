using System;
using NSubstitute;
using NUnit.Framework;

class Fixture
{
    [Test]
    public void Mock()
    {
        using (SubstituteStatic.For<DateTime>())
        {
            var today = DateTime.Today;
            DateTime.Now.Returns(today);

            var c = Substitute.For<Class>();
            c.Add(1, 2).Returns(5);
            
            Assert.Equals(today, DateTime.Now);
            Assert.Equals(3, c.Add(2, 1));
            Assert.Equals(/*5*/6, c.Add(1, 2)); // failing on purpose
        }
    }
}
