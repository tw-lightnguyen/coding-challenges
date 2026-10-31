using CodingChallenge.Share.Messaging;
using CodingChallenge.Share.Models;
using Microsoft.Extensions.Options;

namespace CodingChallenge.DriverManagement.Services;

public sealed class RideCreatedConsumer : RabbitMqConsumer<RideCreatedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RideCreatedConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RideCreatedConsumer> logger,
        IServiceScopeFactory scopeFactory)
        : base(options, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override string QueueName => "driver-management.ride-created";
    protected override string RoutingKey => EventRoutingKeys.RideCreated;

    protected override async Task HandleEventAsync(
        RideCreatedEvent message,
        CancellationToken cancellationToken)
    {
        var pickup = new Coordinate(message.PickupLatitude, message.PickupLongitude);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var matchingService = scope.ServiceProvider.GetRequiredService<DriverMatchingService>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var driverId = await matchingService.FindNearestAvailableDriverId(pickup);
        await publisher.PublishAsync(
            EventRoutingKeys.DriverMatched,
            new DriverMatchedEvent(message.RideId, driverId),
            cancellationToken);
    }
}
