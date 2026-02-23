namespace Yumalog.Tests
{
    using FluentAssertions;
    using System;
    using System.IO;
    using Xunit;
    using Yumalog.Configuration;

    /// <summary>
    /// Tests for CorporateLogConfiguration validation and default values.
    /// </summary>
    [Collection("CorporateLogManager Sequential Tests")]
    public class CorporateLogConfigurationTests
    {
        [Fact]
        public void Configuration_WithValidApplicationName_ShouldSucceed()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp"
            };

            // Assert
            config.ApplicationName.Should().Be("TestApp");
            config.LogDirectory.Should().Contain("TestApp");
        }

        [Fact]
        public void Configuration_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp"
            };

            // Assert
            config.RollingIntervalDays.Should().Be(1, "default rolling interval should be 1 day");
            config.RetainedFileCountLimit.Should().Be(31, "default retained file count should be 31");
            config.FileSizeLimitBytes.Should().Be(100 * 1024 * 1024, "default file size limit should be 100MB");
            config.BufferSize.Should().Be(50000, "default buffer size should be 50000");
            config.BlockWhenFull.Should().BeTrue("default block when full should be true");
        }

        [Fact]
        public void Configuration_CustomValues_ShouldOverrideDefaults()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
                Environment = "Staging",
                BufferSize = 10000,
                RetainedFileCountLimit = 7,
                FileSizeLimitBytes = 50 * 1024 * 1024,
                RollingIntervalDays = 7,
                BlockWhenFull = false
            };

            // Assert
            config.ApplicationName.Should().Be("TestApp");
            config.Environment.Should().Be("Staging");
            config.BufferSize.Should().Be(10000);
            config.RetainedFileCountLimit.Should().Be(7);
            config.FileSizeLimitBytes.Should().Be(50 * 1024 * 1024);
            config.RollingIntervalDays.Should().Be(7);
            config.BlockWhenFull.Should().BeFalse();
        }

        [Fact]
        public void Validate_WithValidApplicationName_ShouldNotThrowException()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "ValidAppName"
            };

            // Act & Assert
            Action act = () => config.Validate();
            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_WithNullApplicationName_ShouldThrowArgumentException()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = null
            };

            // Act & Assert
            Action act = () => config.Validate();
            act.Should().Throw<ArgumentException>()
                .WithMessage("*ApplicationName*");
        }

        [Fact]
        public void Validate_WithEmptyApplicationName_ShouldThrowArgumentException()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = string.Empty
            };

            // Act & Assert
            Action act = () => config.Validate();
            act.Should().Throw<ArgumentException>()
                .WithMessage("*ApplicationName*");
        }

        [Fact]
        public void Validate_WithWhitespaceApplicationName_ShouldThrowArgumentException()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "   "
            };

            // Act & Assert
            Action act = () => config.Validate();
            act.Should().Throw<ArgumentException>()
                .WithMessage("*ApplicationName*");
        }

        [Fact]
        public void LogDirectory_ShouldCombineBaseDirectoryAndApplicationName()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "MyCustomApp"
            };

            // Assert
            config.LogDirectory.Should().EndWith("MyCustomApp");
            config.LogDirectory.Should().StartWith(@"C:\ServiceLogs");
            config.LogDirectory.Should().Be(Path.Combine(@"C:\ServiceLogs", "MyCustomApp"));
        }

        [Fact]
        public void BaseLogDirectory_ShouldBeFixedPath()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp"
            };

            // Assert
            config.BaseLogDirectory.Should().Be(@"C:\ServiceLogs");
        }

        [Theory]
        [InlineData("App1")]
        [InlineData("MyService")]
        [InlineData("WebAPI-v2")]
        [InlineData("Background_Worker")]
        public void LogDirectory_WithDifferentApplicationNames_ShouldBuildCorrectPath(string appName)
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = appName
            };

            // Assert
            config.LogDirectory.Should().Be(Path.Combine(@"C:\ServiceLogs", appName));
        }

        [Fact]
        public void Environment_WhenExplicitlySet_ShouldReturnSetValue()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
                Environment = "Production"
            };

            // Assert - Before validation
            config.Environment.Should().Be("Production");

            // Act - Validate
            config.Validate();

            // Assert - After validation (should not change)
            config.Environment.Should().Be("Production",
                "explicitly set environment should not be overridden");
        }

        [Fact]
        public void Environment_WhenNotSet_ShouldReturnNullBeforeValidation()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp"
                // Environment not set
            };

            // Assert
            config.Environment.Should().BeNull("environment is null before Validate() is called");
        }

        [Fact]
        public void Environment_WhenNotSetAndValidated_ShouldAutoDetect()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp"
                // Environment not set - will auto-detect during Validate()
            };

            // Act
            config.Validate();

            // Assert
            config.Environment.Should().NotBeNullOrWhiteSpace("environment should be auto-detected after Validate()");
            config.Environment.Should().BeOneOf("Development", "Staging", "Production",
                "because auto-detect checks ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, or defaults to Production");
        }

        [Fact]
        public void Environment_WhenExplicitlySet_ShouldRetainValueAfterValidation()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
                Environment = "CustomEnvironment"
            };

            // Act
            config.Validate();

            // Assert
            config.Environment.Should().Be("CustomEnvironment",
                "explicitly set environment should not be overridden by auto-detection");
        }

        [Fact]
        public void BufferSize_WithZeroValue_ShouldBeAllowed()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
                BufferSize = 0 // Synchronous logging (no buffer)
            };

            // Assert
            config.BufferSize.Should().Be(0);
        }

        [Fact]
        public void BufferSize_WithLargeValue_ShouldBeAllowed()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
                BufferSize = 100000 // Large buffer for high-volume scenarios
            };

            // Assert
            config.BufferSize.Should().Be(100000);
        }

        [Fact]
        public void FileSizeLimitBytes_WithDefaultValue_ShouldBe100MB()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp"
            };

            // Assert
            config.FileSizeLimitBytes.Should().Be(100 * 1024 * 1024, "default should be 100MB");
        }

        [Fact]
        public void FileSizeLimitBytes_WithNullValue_ShouldAllowUnlimitedFileSize()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
                FileSizeLimitBytes = null // No size limit
            };

            // Assert
            config.FileSizeLimitBytes.Should().BeNull();
        }

        [Fact]
        public void RetainedFileCountLimit_WithNullValue_ShouldRetainAllFiles()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
            };

            // Assert
            config.RetainedFileCountLimit.Should().Be(31);
        }

        [Fact]
        public void RetainedFileCountLimit_WithCustomValue_ShouldRetainSpecifiedCount()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "TestApp",
                RetainedFileCountLimit = 7
            };

            // Assert
            config.RetainedFileCountLimit.Should().Be(7);
        }

        [Fact]
        public void Configuration_WithMinimalSettings_ShouldBeValid()
        {
            // Arrange & Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = "MinimalApp"
                // All other settings use defaults
            };

            // Assert
            Action act = () => config.Validate();
            act.Should().NotThrow();

            config.ApplicationName.Should().Be("MinimalApp");
            config.LogDirectory.Should().Contain("MinimalApp");
            config.BufferSize.Should().BeGreaterThan(0);
            config.RetainedFileCountLimit.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Configuration_WithAllCustomSettings_ShouldPreserveAllValues()
        {
            // Arrange
            var expectedAppName = "CompleteApp";
            var expectedEnvironment = "QA";
            var expectedBufferSize = 25000;
            var expectedRetainedFiles = 14;
            var expectedFileSizeLimit = 200 * 1024 * 1024; // 200MB
            var expectedRollingDays = 30;

            // Act
            var config = new CorporateLogConfiguration
            {
                ApplicationName = expectedAppName,
                Environment = expectedEnvironment,
                BufferSize = expectedBufferSize,
                RetainedFileCountLimit = expectedRetainedFiles,
                FileSizeLimitBytes = expectedFileSizeLimit,
                RollingIntervalDays = expectedRollingDays,
                BlockWhenFull = false
            };

            // Assert
            config.ApplicationName.Should().Be(expectedAppName);
            config.Environment.Should().Be(expectedEnvironment);
            config.BufferSize.Should().Be(expectedBufferSize);
            config.RetainedFileCountLimit.Should().Be(expectedRetainedFiles);
            config.FileSizeLimitBytes.Should().Be(expectedFileSizeLimit);
            config.RollingIntervalDays.Should().Be(expectedRollingDays);
            config.BlockWhenFull.Should().BeFalse();
        }
    }
}