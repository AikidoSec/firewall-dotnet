using System.Reflection;

using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    /// <summary>
    /// Tests for the ReflectionHelper class.
    /// </summary>
    public class ReflectionHelperTests
    {
        [SetUp]
        public void SetUp()
        {
            // Clear the cache before each test to ensure a clean state
            ReflectionHelper.ClearCache();
        }

        [Test]
        public void GetMethodFromAssembly_ValidMethod_ReturnsMethodInfo()
        {
            // Arrange
            var assemblyName = "System.Private.CoreLib";
            var typeName = "System.String";
            var methodName = "Contains";
            var parameterTypeNames = new[] { "System.String" };

            // Act
            var methodInfo = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);

            // Assert
            Assert.That(methodInfo, Is.Not.Null);
            Assert.That(methodInfo.Name, Is.EqualTo(methodName));
        }

        [Test]
        public void GetMethodFromAssembly_InvalidAssembly_ReturnsNull()
        {
            // Arrange
            var assemblyName = "NonExistentAssembly";
            var typeName = "System.String";
            var methodName = "Contains";
            var parameterTypeNames = new[] { "System.String" };

            // Act
            var methodInfo = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);

            // Assert
            Assert.That(methodInfo, Is.Null);
        }

        [Test]
        public void GetMethodFromAssembly_InvalidType_ReturnsNull()
        {
            // Arrange
            var assemblyName = "System.Private.CoreLib";
            var typeName = "NonExistentType";
            var methodName = "Contains";
            var parameterTypeNames = new[] { "System.String" };

            // Act
            var methodInfo = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);

            // Assert
            Assert.That(methodInfo, Is.Null);
        }

        [Test]
        public void GetMethodFromAssembly_InvalidMethod_ReturnsNull()
        {
            // Arrange
            var assemblyName = "System.Private.CoreLib";
            var typeName = "System.String";
            var methodName = "NonExistentMethod";
            var parameterTypeNames = new[] { "System.String" };

            // Act
            var methodInfo = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);

            // Assert
            Assert.That(methodInfo, Is.Null);
        }

        [Test]
        public void GetMethodFromType_ValidMethod_ReturnsMethodInfo()
        {
            var methodInfo = ReflectionHelper.GetMethodFromType(
                typeof(string),
                nameof(string.Contains),
                "System.String");

            Assert.That(methodInfo, Is.Not.Null);
            Assert.That(methodInfo.Name, Is.EqualTo(nameof(string.Contains)));
        }

        [Test]
        public void ClearCache_ClearsAllCachedData()
        {
            // Arrange
            var assemblyName = "System.Private.CoreLib";
            var typeName = "System.String";
            var methodName = "Contains";
            var parameterTypeNames = new[] { "System.String" };

            // Act
            var methodInfo = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            ReflectionHelper.ClearCache();
            var methodInfoAfterClear = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);

            // Assert
            Assert.That(methodInfo, Is.Not.Null);
            Assert.That(methodInfoAfterClear, Is.Not.Null);
            Assert.That(methodInfo, Is.EqualTo(methodInfoAfterClear));
        }

        [Test]
        public void GetMethodModule_ReturnsSimpleAssemblyName()
        {
            var method = typeof(string).GetMethod("Contains", new[] { typeof(string) });

            var module = ReflectionHelper.GetMethodModule(method);

            Assert.That(module, Is.EqualTo(typeof(string).Assembly.GetName().Name));
            Assert.That(module, Does.Not.Contain(","));
        }

        [Test]
        public void GetMemberValue_ReturnsPropertyOrFieldFromTypeHierarchy()
        {
            var instance = new DerivedReflectionTarget();

            Assert.That(ReflectionHelper.GetStringMember(instance, "PublicText"), Is.EqualTo("public"));
            Assert.That(ReflectionHelper.GetStringMember(instance, "PrivateText"), Is.EqualTo("private"));
            Assert.That(ReflectionHelper.GetMemberValue(instance, "InheritedField"), Is.EqualTo(42));
            Assert.That(ReflectionHelper.GetMemberValue(instance, "Missing"), Is.Null);
            Assert.That(ReflectionHelper.GetMemberValue(null, "PublicText"), Is.Null);
        }

        [Test]
        public void GetMethodFromAssembly_PatchTraversal_ReturnsNull()
        {
            // Arrange
            var assemblyName = "..\\Aikido.Zen.DotNetFramework\\bin\\Debug\\net48\\Aikido.Zen.DotNetFramework.dll";
            var typeName = "Aikido.Zen.DotNetFramework.ContextModule";
            var methodName = "Init";
            var parameterTypeNames = new[] { "System.Web.HttpApplication" };

            // Act
            var methodInfo = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);

            // Assert
            Assert.That(methodInfo, Is.Null);
        }

        private class BaseReflectionTarget
        {
            public int InheritedField = 42;
        }

        private class DerivedReflectionTarget : BaseReflectionTarget
        {
            public string PublicText { get; set; } = "public";
            private string PrivateText { get; set; } = "private";
        }

    }
}
