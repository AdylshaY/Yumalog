namespace Yumalog.Tests
{
    using FluentAssertions;
    using System;
    using Xunit;
    using Yumalog.Configuration;

    /// <summary>
    /// Tests for CorporateLogManager static manager initialization and lifecycle.
    /// </summary>
    [Collection("CorporateLogManager Sequential Tests")]
    public class CorporateLogManagerTests : IDisposable
    {
        public void Dispose()
        {
            if (CorporateLogManager.IsInitialized)
            {
                CorporateLogManager.Shutdown();
            }
        }

        /// <summary>
        /// Verifies that initializing the CorporateLogManager with a valid application name completes successfully and sets the expected state.
        /// </summary>
        /// <remarks>This test ensures that after calling Initialize with a valid application name, the
        /// CorporateLogManager is marked as initialized and the Current instance is not null.</remarks>
        [Fact]
        public void Initialize_WithValidApplicationName_ShouldSucceed()
        {
            // Arrange
            string applicationName = "TestApp";
            // Act
            CorporateLogManager.Initialize(applicationName);
            // Assert
            CorporateLogManager.IsInitialized.Should().BeTrue();
            CorporateLogManager.Current.Should().NotBeNull();
        }

        [Fact]
        public void Initialize_WithNullApplicationName_ShouldThrowArgumentException()
        {
            // Arrange
            string applicationName = null;
            // Act
            Action act = () => CorporateLogManager.Initialize(applicationName);
            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName("applicationName");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Initialize_WithInvalidApplicationName_ShouldThrowArgumentException(string appName)
        {
            // Act & Assert
            Action act = () => CorporateLogManager.Initialize(appName);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("applicationName");
        }

        [Fact]
        public void Initialize_TwiceWithoutShutdown_ShouldThrowInvalidOperationException()
        {
            // Arrange
            CorporateLogManager.Initialize("TestApp");

            // Act & Assert
            Action act = () => CorporateLogManager.Initialize("TestApp2");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*already been initialized*");
        }

        [Fact]
        public void Current_BeforeInitialize_ShouldThrowInvalidOperationException()
        {
            // Act & Assert
            Action act = () => { var logger = CorporateLogManager.Current; };

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*has not been initialized*");
        }

        [Fact]
        public void Shutdown_AfterInitialize_ShouldAllowReinitialization()
        {
            // Arrange
            CorporateLogManager.Initialize("TestApp1");
            CorporateLogManager.Shutdown();

            // Act - İkinci kez initialize edilebilmeli
            Action act = () => CorporateLogManager.Initialize("TestApp2");

            // Assert
            act.Should().NotThrow();
            CorporateLogManager.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void Shutdown_WithoutInitialize_ShouldNotThrowException()
        {
            // Act & Assert
            Action act = () => CorporateLogManager.Shutdown();

            act.Should().NotThrow();
        }

        [Fact]
        public void IsInitialized_BeforeInitialize_ShouldBeFalse()
        {
            // Assert
            CorporateLogManager.IsInitialized.Should().BeFalse();
        }

        [Fact]
        public void IsInitialized_AfterInitialize_ShouldBeTrue()
        {
            // Act
            CorporateLogManager.Initialize("TestApp");

            // Assert
            CorporateLogManager.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void IsInitialized_AfterShutdown_ShouldBeFalse()
        {
            // Arrange
            CorporateLogManager.Initialize("TestApp");

            // Act
            CorporateLogManager.Shutdown();

            // Assert
            CorporateLogManager.IsInitialized.Should().BeFalse();
        }


        [Fact]
        public void Initialize_WithCustomConfiguration_ShouldSucceed()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "CustomApp",
                Environment = "Testing",
                BufferSize = 10000,
                RetainedFileCountLimit = 7
            };

            // Act
            CorporateLogManager.Initialize(config);

            // Assert
            CorporateLogManager.IsInitialized.Should().BeTrue();
            CorporateLogManager.Current.Should().NotBeNull();
        }

        [Fact]
        public void Initialize_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange
            CorporateLogConfiguration config = null;

            // Act & Assert
            Action act = () => CorporateLogManager.Initialize(config);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("configuration");
        }
    }
}
