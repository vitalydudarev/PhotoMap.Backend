using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using PhotoMap.Shared.Messaging;

namespace PhotoMap.Api.Services.Tests;

public class KafkaClientsTests
{
    [Fact]
    public void Configure_ShouldConnectToTheBrokersOfTheSettings_BackingOffBetweenAttempts()
    {
        // Act
        var config = KafkaClients.Configure(new ConsumerConfig { GroupId = "group" },
            new KafkaSettings { BootstrapServers = "kafka:9092" });

        // Assert
        Assert.Equal("kafka:9092", config.BootstrapServers);
        Assert.Equal("group", config.GroupId);
        Assert.Equal(1000, config.ReconnectBackoffMs);
        Assert.Equal(10000, config.ReconnectBackoffMaxMs);
    }

    [Fact]
    public void LogError_ShouldLogTheErrorsOfTheConnectionAtDebug_NotToFloodTheLog()
    {
        // Arrange
        var logger = new Mock<ILogger>();

        // Act: what every attempt to connect to a broker that is down reports
        KafkaClients.LogError(logger.Object, "producer", new Error(ErrorCode.Local_Transport, "Connection refused", false));
        KafkaClients.LogError(logger.Object, "producer", new Error(ErrorCode.Local_AllBrokersDown, "1/1 brokers are down", false));

        // Assert
        logger.Verify(a => a.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(2));
        logger.Verify(a => a.Log(It.Is<LogLevel>(b => b > LogLevel.Debug), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public void LogError_ShouldLogAFatalErrorAsAnError()
    {
        // Arrange
        var logger = new Mock<ILogger>();

        // Act
        KafkaClients.LogError(logger.Object, "producer", new Error(ErrorCode.Local_Fatal, "Fatal idempotent producer error", true));

        // Assert
        logger.Verify(a => a.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
