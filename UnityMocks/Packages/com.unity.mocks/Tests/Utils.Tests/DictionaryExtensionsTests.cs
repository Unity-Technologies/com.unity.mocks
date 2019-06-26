using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shouldly;
using Unity.Utils;

namespace Utils
{
    class DictionaryExtensionsTests
    {
        [Test]
        public void OrEmpty_NonNullInput_ReturnsInput()
        {
            var dictionary = new Dictionary<int, string> {[0] = "zero" };

            dictionary.OrEmpty().ShouldBe(dictionary);
        }

        [Test]
        public void OrEmpty_NullInput_ReturnsEmpty()
        {
            IReadOnlyDictionary<string, int> dictionary = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            dictionary.OrEmpty().ShouldBeEmpty();
        }

        [Test]
        public void GetValueOr_Found_ReturnsFound()
        {
            var dictionary = new Dictionary<int, string> {[1] = "one" };

            dictionary.GetValueOr(1).ShouldBe("one");
            dictionary.GetValueOr(1, "two").ShouldBe("one");
        }

        [Test]
        public void GetValueOr_NotFound_ReturnsDefault()
        {
            var dictionary = new Dictionary<string, int> {["one"] = 1 };

            dictionary.GetValueOr("two").ShouldBe(0);
            dictionary.GetValueOr("two", 2).ShouldBe(2);
        }

        [Test]
        public void AddRange_NonDuplicatedKeys_ReturnsCombinedDictionaries()
        {
            var dict = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 };
            var other = new Dictionary<string, int> { ["three"] = 3 };

            var combined = dict.AddRangeOverride(other);

            Assert.That(combined.Count, Is.EqualTo(3), "Combined dictionary should have 3 elements.");

            combined.ShouldContainKeyAndValue("one", 1);
            combined.ShouldContainKeyAndValue("two", 2);
            combined.ShouldContainKeyAndValue("three", 3);
        }

        [Test]
        public void AddRange_UpdatesEntries_IfAlreadyExists()
        {
            var dict = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 };
            var other = new Dictionary<string, int> { ["two"] = 22 };
            dict.AddRangeOverride(other).ShouldContain(pair => pair.Key == "two" && pair.Value == 22);
        }
    }
}
