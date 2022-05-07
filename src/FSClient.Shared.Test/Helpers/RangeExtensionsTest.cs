namespace FSClient.Shared.Test.Helpers
{
    using System;

    using FSClient.Shared.Helpers;

    using NUnit.Framework;

    [TestFixture]
    public class RangeExtensionsTest
    {
        [TestCase(1)]
        public void Should_Not_Have_Range(int start)
        {
            var hasRange = new Range(start, start + 1).HasRange();
            Assert.That(hasRange, Is.False);
        }

        [TestCase(1, 5)]
        public void Should_Have_Range(int start, int end)
        {
            var hasRange = new Range(start, end).HasRange();
            Assert.That(hasRange, Is.True);
        }

        [TestCase(1, 5, 2, 3)]
        [TestCase(1, 5, 4, 5)]
        [TestCase(4, 5, 1, 4)]
        [TestCase(4, 5, 1, 5)]
        [TestCase(1, 1, 1, 1)]
        public void Should_Be_IsIntersected(int start1, int end1, int start2, int end2)
        {
            var range1 = new Range(start1, end1);
            var range2 = new Range(start2, end2);
            var isIntersected = range1.IsIntersected(range2);
            Assert.That(isIntersected, Is.True);
        }

        [TestCase(1, 2, 3, 4)]
        [TestCase(1, 2, 2, 3)]
        [TestCase(3, 4, 1, 2)]
        public void Should_Not_Be_IsIntersected(int start1, int end1, int start2, int end2)
        {
            var range1 = new Range(start1, end1);
            var range2 = new Range(start2, end2);
            var isIntersected = range1.IsIntersected(range2);
            Assert.That(isIntersected, Is.False);
        }

        [TestCase(1, 2, 2, 3)]
        [TestCase(3, 4, 2, 3)]
        public void Should_Be_Near(int start1, int end1, int start2, int end2)
        {
            var range1 = new Range(start1, end1);
            var range2 = new Range(start2, end2);
            var isNear = range1.IsNear(range2);
            Assert.That(isNear, Is.True);
        }

        [TestCase(1, 2, 3, 4)]
        [TestCase(3, 4, 1, 2)]
        public void Should_Not_Be_Near(int start1, int end1, int start2, int end2)
        {
            var range1 = new Range(start1, end1);
            var range2 = new Range(start2, end2);
            var isNear = range1.IsNear(range2);
            Assert.That(isNear, Is.False);
        }

        [TestCase(1)]
        public void Should_Convert_To_Range(int input)
        {
            var range = input.ToRange();
            Assert.That(range.Start, Is.EqualTo(new Index(input)));
            Assert.That(range.End, Is.EqualTo(new Index(input + 1)));
        }

        [TestCase("1 - 2", 1, 3)]
        [TestCase("1", 1, 2)]
        public void Should_Convert_To_FormattedString(string expected, int start, int end)
        {
            var range = new Range(start, end);
            Assert.That(range.ToFormattedString(), Is.EqualTo(expected));
        }

        [TestCase("1 - 2", 1, 3)]
        [TestCase("1 10", 1, 11)]
        [TestCase("4 , 12", 4, 13)]
        [TestCase("4..12", 4, 13)]
        [TestCase("3", 3, 4)]
        public void Should_Convert_From_FormattedString(string input, int expStart, int expEnd)
        {
            var result = RangeExtensions.TryParse(input, out var range);
            Assert.That(result, Is.True);
            Assert.That(range.Start.Value, Is.EqualTo(expStart));
            Assert.That(range.End.Value, Is.EqualTo(expEnd));
        }

        [TestCase("NaN")]
        [TestCase("4 = 5")]
        [TestCase("4.12")]
        [TestCase(null)]
        [TestCase("4 - ")]
        public void Should_Not_Convert_From_FormattedString(string? input)
        {
            var result = RangeExtensions.TryParse(input, out var range);
            Assert.That(result, Is.False);
            Assert.That(range, Is.EqualTo(default(Range)));
        }
    }
}
