namespace Yumalog.Tests
{
    using FluentAssertions;
    using System;
    using Xunit;
    using Yumalog.Configuration;

    /// <summary>
    /// Tests for YumalogManager static manager initialization and lifecycle.
    /// </summary>
    [Collection("YumalogManager Sequential Tests")]
    public class YumalogManagerTests : IDisposable
    {
        public void Dispose()
        {
            if (YumalogManager.IsInitialized)
            {
                YumalogManager.Shutdown();
            }
        }

        /// <summary>
        /// Verifies that initializing the YumalogManager with a valid application name completes successfully and sets the expected state.
        /// </summary>
        /// <remarks>This test ensures that after calling Initialize with a valid application name, the
        /// YumalogManager is marked as initialized and the Current instance is not null.</remarks>
        [Fact]
        public void Initialize_WithValidApplicationName_ShouldSucceed()
        {
            // Arrange
            string applicationName = "TestApp";
            // Act
            YumalogManager.Initialize(applicationName);
            // Assert
            YumalogManager.IsInitialized.Should().BeTrue();
            YumalogManager.Current.Should().NotBeNull();
        }

        [Fact]
        public void Initialize_WithNullApplicationName_ShouldThrowArgumentException()
        {
            // Arrange
            string applicationName = null;
            // Act
            Action act = () => YumalogManager.Initialize(applicationName);
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
            Action act = () => YumalogManager.Initialize(appName);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("applicationName");
        }

        [Fact]
        public void Initialize_TwiceWithoutShutdown_ShouldThrowInvalidOperationException()
        {
            // Arrange
            YumalogManager.Initialize("TestApp");

            // Act & Assert
            Action act = () => YumalogManager.Initialize("TestApp2");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*already been initialized*");
        }

        [Fact]
        public void Current_BeforeInitialize_ShouldThrowInvalidOperationException()
        {
            // Act & Assert
            Action act = () => { var logger = YumalogManager.Current; };

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*has not been initialized*");
        }

        [Fact]
        public void Shutdown_AfterInitialize_ShouldAllowReinitialization()
        {
            // Arrange
            YumalogManager.Initialize("TestApp1");
            YumalogManager.Shutdown();

            // Act - İkinci kez initialize edilebilmeli
            Action act = () => YumalogManager.Initialize("TestApp2");

            // Assert
            act.Should().NotThrow();
            YumalogManager.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void Shutdown_WithoutInitialize_ShouldNotThrowException()
        {
            // Act & Assert
            Action act = () => YumalogManager.Shutdown();

            act.Should().NotThrow();
        }

        [Fact]
        public void IsInitialized_BeforeInitialize_ShouldBeFalse()
        {
            // Assert
            YumalogManager.IsInitialized.Should().BeFalse();
        }

        [Fact]
        public void IsInitialized_AfterInitialize_ShouldBeTrue()
        {
            // Act
            YumalogManager.Initialize("TestApp");

            // Assert
            YumalogManager.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void IsInitialized_AfterShutdown_ShouldBeFalse()
        {
            // Arrange
            YumalogManager.Initialize("TestApp");

            // Act
            YumalogManager.Shutdown();

            // Assert
            YumalogManager.IsInitialized.Should().BeFalse();
        }


        [Fact]
        public void Initialize_WithCustomConfiguration_ShouldSucceed()
        {
            // Arrange
            var config = new YumalogConfiguration
            {
                ApplicationName = "CustomApp",
                Environment = "Testing",
                BufferSize = 10000,
                RetainedFileCountLimit = 7
            };

            // Act
            YumalogManager.Initialize(config);

            // Assert
            YumalogManager.IsInitialized.Should().BeTrue();
            YumalogManager.Current.Should().NotBeNull();
        }

        [Fact]
        public void Initialize_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange
            YumalogConfiguration config = null;

            // Act & Assert
            Action act = () => YumalogManager.Initialize(config);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("configuration");
        }
    }
}
