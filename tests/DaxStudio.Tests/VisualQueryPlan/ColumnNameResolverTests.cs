using DaxStudio.UI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.VisualQueryPlan
{
    [TestClass]
    public class ColumnNameResolverTests
    {
        private ColumnNameResolver _resolver;

        [TestInitialize]
        public void TestSetup()
        {
            _resolver = new ColumnNameResolver();
        }

        [TestMethod]
        public void ResolveColumnName_WhenNotInitialized_ReturnsOriginal()
        {
            // Arrange
            var columnRef = "$Column12345";

            // Act
            var result = _resolver.ResolveColumnName(columnRef);

            // Assert
            Assert.AreEqual(columnRef, result);
        }

        [TestMethod]
        public void ResolveColumnName_WithNullInput_ReturnsNull()
        {
            // Act
            var result = _resolver.ResolveColumnName(null);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ResolveColumnName_WithEmptyInput_ReturnsEmpty()
        {
            // Act
            var result = _resolver.ResolveColumnName(string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ResolveOperationString_WhenNotInitialized_ReturnsOriginal()
        {
            // Arrange
            var operation = "AddColumns: RelLogOp DependOnCols($Column123)";

            // Act
            var result = _resolver.ResolveOperationString(operation);

            // Assert
            Assert.AreEqual(operation, result);
        }

        [TestMethod]
        public void IsInitialized_WhenNotInitialized_ReturnsFalse()
        {
            // Assert
            Assert.IsFalse(_resolver.IsInitialized);
        }
    }
}
