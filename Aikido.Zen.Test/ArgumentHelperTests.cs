using System.Reflection;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class ArgumentHelperTests
    {
        // Test subjects
        public class TestMethods
        {
            public void Simple(int a, string b) { }
            public void WithOptional(int a, string b = "default") { }
            public void WithRef(ref int a, string b) { }
            public void WithOut(out int a, string b) { a = 1; }
            public void WithParams(int a, params string[] b) { }
            public void WithMixed(int a, ref int b, out int c, string d = "d_val", params object[] e) { c = 3; }
            public static void StaticMethod(int a, string b) { }
        }

        [Test]
        public void BuildArgumentDictionary_SimpleArguments_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.Simple));
            var args = new object[] { 42, "hello" };
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo(42));
            Assert.That(result["b"], Is.EqualTo("hello"));
        }

        [Test]
        public void BuildArgumentDictionary_WithOptional_ArgumentProvided_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithOptional));
            var args = new object[] { 42, "provided" };
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo(42));
            Assert.That(result["b"], Is.EqualTo("provided"));
        }

        [Test]
        public void BuildArgumentDictionary_WithOptional_ArgumentNotProvided_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithOptional));
            var args = new object[] { 42 };
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo(42));
            Assert.That(result["b"], Is.EqualTo("default"));
        }

        [Test]
        public void BuildArgumentDictionary_WithRef_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithRef));
            var args = new object[] { 42, "hello" };
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo(42));
            Assert.That(result["b"], Is.EqualTo("hello"));
        }

        [Test]
        public void BuildArgumentDictionary_WithOut_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithOut));
            var args = new object[] { "hello" }; // 'out' param is not in args
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.Null);
            Assert.That(result["b"], Is.EqualTo("hello"));
        }

        [Test]
        public void BuildArgumentDictionary_WithParams_ParamsProvided_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithParams));
            var args = new object[] { 42, "one", "two", "three" };
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo(42));
            Assert.That(result["b"], Is.TypeOf<string[]>());
            var bArray = (string[])result["b"];
            Assert.That(bArray.Length, Is.EqualTo(3));
            Assert.That(bArray[0], Is.EqualTo("one"));
        }

        [Test]
        public void BuildArgumentDictionary_WithParams_ParamsNotProvided_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithParams));
            var args = new object[] { 42 };
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo(42));
            Assert.That(result["b"], Is.TypeOf<string[]>());
            Assert.That(((string[])result["b"]).Length, Is.EqualTo(0));
        }

        [Test]
        public void BuildArgumentDictionary_WithMixedParameters_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithMixed));
            var args = new object[] { 1, 2, "hello", 100, 200.5 }; // a, b, d, e...
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(5));
            Assert.That(result["a"], Is.EqualTo(1));
            Assert.That(result["b"], Is.EqualTo(2)); // ref
            Assert.That(result["c"], Is.Null); // out
            Assert.That(result["d"], Is.EqualTo("hello")); // optional provided
            Assert.That(result["e"], Is.TypeOf<object[]>());
            var eArray = (object[])result["e"];
            Assert.That(eArray.Length, Is.EqualTo(2));
            Assert.That(eArray[0], Is.EqualTo(100));
        }

        [Test]
        public void BuildArgumentDictionary_WithMixedParameters_OptionalAndParamsNotProvided_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.WithMixed));
            var args = new object[] { 1, 2 }; // a, b
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(5));
            Assert.That(result["a"], Is.EqualTo(1));
            Assert.That(result["b"], Is.EqualTo(2)); // ref
            Assert.That(result["c"], Is.Null); // out
            Assert.That(result["d"], Is.EqualTo("d_val")); // optional not provided
            Assert.That(result["e"], Is.TypeOf<object[]>());
            Assert.That(((object[])result["e"]).Length, Is.EqualTo(0)); // params not provided
        }

        [Test]
        public void BuildArgumentDictionary_ForStaticMethod_BuildsCorrectDictionary()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.StaticMethod));
            var args = new object[] { 123, "static" };
            var result = ArgumentHelper.BuildArgumentDictionary(args, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo(123));
            Assert.That(result["b"], Is.EqualTo("static"));
        }

        [Test]
        public void BuildArgumentDictionary_WithNullArgs_ReturnsEmptyValues()
        {
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.Simple));
            var result = ArgumentHelper.BuildArgumentDictionary(null, method);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.Null);
            Assert.That(result["b"], Is.Null);
        }
    }
}
