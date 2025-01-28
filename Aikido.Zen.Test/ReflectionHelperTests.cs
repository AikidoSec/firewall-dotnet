using Aikido.Zen.Core.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

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
    }
}
