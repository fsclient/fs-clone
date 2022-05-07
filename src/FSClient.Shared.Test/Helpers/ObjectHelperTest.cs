namespace FSClient.Shared.Test.Helpers
{
    using System;

    using FSClient.Shared.Helpers;

    using NUnit.Framework;

    [TestFixture]
    public class ObjectHelperTest
    {
        [TestCase("hello world", "empty", "hello world", typeof(string))]
        [TestCase(null, "empty", "empty", typeof(string))]
        [TestCase(null, null, null, typeof(string))]
        public void Should_Complete_Safe_Cast_String(object? input, object? otherwise, object? expected, Type expectedType)
        {
            Should_Complete_Safe_Cast(input, otherwise, expected, expectedType);
        }

        [TestCase(1, default(int), 1, typeof(int))]
        [TestCase(null, 2, 2, typeof(int))]
        [TestCase(1L, default(int), 1, typeof(int))]
        [TestCase(1, default(long), 1L, typeof(long))]
        public void Should_Complete_Safe_Cast_Number(object? input, object? otherwise, object? expected, Type expectedType)
        {
            Should_Complete_Safe_Cast(input, otherwise, expected, expectedType);
        }

        [TestCase(1f, default(float), 1f, typeof(float))]
        [TestCase(1L, default(float), 1f, typeof(float))]
        [TestCase(null, 2f, 2f, typeof(float))]
        [TestCase(1d, default(float), 1f, typeof(float))]
        [TestCase(1f, default(double), 1d, typeof(double))]
        public void Should_Complete_Safe_Cast_FloatNumber(object? input, object? otherwise, object? expected, Type expectedType)
        {
            Should_Complete_Safe_Cast(input, otherwise, expected, expectedType);
        }

        [TestCase("1.1", default(float), 1.1f, typeof(float))]
        [TestCase("1.1", default(double), 1.1d, typeof(double))]
        public void Should_Complete_Safe_Cast_StringToFloatNumber(object? input, object? otherwise, object? expected, Type expectedType)
        {
            Should_Complete_Safe_Cast(input, otherwise, expected, expectedType);
        }

        [TestCase(TestEnum.One, default(TestEnum), TestEnum.One, typeof(TestEnum))]
        [TestCase(TestLongEnum.One, default(TestLongEnum), TestLongEnum.One, typeof(TestLongEnum))]
        [TestCase(null, TestEnum.Two, TestEnum.Two, typeof(TestEnum))]
        [TestCase(TestLongEnum.One, default(TestEnum), TestEnum.One, typeof(TestEnum))]
        public void Should_Complete_Safe_Cast_Enum(object? input, object? otherwise, object? expected, Type expectedType)
        {
            Should_Complete_Safe_Cast(input, otherwise, expected, expectedType);
        }

        [TestCase(1, default(TestEnum), TestEnum.One, typeof(TestEnum))]
        [TestCase(1, default(TestLongEnum), TestLongEnum.One, typeof(TestLongEnum))]
        [TestCase(1L, default(TestLongEnum), TestLongEnum.One, typeof(TestLongEnum))]
        [TestCase(1L, default(TestEnum), TestEnum.One, typeof(TestEnum))]
        public void Should_Complete_Safe_Cast_NumberToEnum(object? input, object? otherwise, object? expected, Type expectedType)
        {
            Should_Complete_Safe_Cast(input, otherwise, expected, expectedType);
        }

        [Test]
        public void Should_Complete_Safe_Cast_Nullable_ExptectedNull()
        {
            Should_Complete_Safe_Cast(null, (int?)null, null, typeof(int?));
        }

        [Test]
        public void Should_Complete_Safe_Cast_Nullable_OtherwiseWithValue()
        {
            Should_Complete_Safe_Cast(null, (int?)1, 1, typeof(int));
        }

        [Test]
        public void Should_Complete_Safe_Cast_Nullable_FromValue()
        {
            Should_Complete_Safe_Cast(1, default(int?), 1, typeof(int));
        }

        private void Should_Complete_Safe_Cast(object? input, object? otherwise, object? expected, Type expectedType)
        {
            var methodInfo = typeof(ObjectHelper).GetMethod(nameof(ObjectHelper.SafeCast))!.MakeGenericMethod(expectedType);

            var output = methodInfo.Invoke(null, new[] { input, otherwise });

            Assert.That(output, Is.EqualTo(expected));
            if (output != null)
            {
                Assert.That(output.GetType(), Is.EqualTo(expectedType));
            }
        }

        private enum TestEnum : int
        {
            One = 1,
            Two
        }

        private enum TestLongEnum : long
        {
            One = 1,
            Two
        }
    }
}
