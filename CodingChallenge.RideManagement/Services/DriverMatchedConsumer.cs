using CodingChallenge.RideManagement.Data;
using CodingChallenge.Share.Messaging;
using Microsoft.Extensions.Options;

namespace CodingChallenge.RideManagement.Services;

public sealed class DriverMatchedConsumer : RabbitMqConsumer<DriverMatchedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DriverMatchedConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<DriverMatchedConsumer> logger,
        IServiceScopeFactory scopeFactory)
        : base(options, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override string QueueName => "ride-management.driver-matched";
    protected override string RoutingKey => EventRoutingKeys.DriverMatched;

    protected override async Task HandleEventAsync(
        DriverMatchedEvent message,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ride = await dbContext.Rides.FindAsync([message.RideId], cancellationToken);
        if (ride == null)
        {
            throw new InvalidOperationException($"Ride {message.RideId} was not found.");
        }

        ride.AssignedDriverId = message.DriverId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
