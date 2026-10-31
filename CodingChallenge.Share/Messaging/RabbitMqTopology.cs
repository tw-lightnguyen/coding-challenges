using RabbitMQ.Client;

namespace CodingChallenge.Share.Messaging;

public static class RabbitMqTopology
{
    public static void Declare(IModel channel)
    {
        channel.ExchangeDeclare(RabbitMqEventPublisher.ExchangeName, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(
            $"{RabbitMqEventPublisher.ExchangeName}.dead",
            ExchangeType.Direct,
            durable: true);

        DeclareQueue(channel, "driver-management.ride-created", EventRoutingKeys.RideCreated);
        DeclareQueue(channel, "ride-management.driver-matched", EventRoutingKeys.DriverMatched);
    }

    public static void DeclareQueue(IModel channel, string queueName, string routingKey)
    {
        var deadLetterExchange = $"{RabbitMqEventPublisher.ExchangeName}.dead";
        var deadLetterQueue = $"{queueName}.dead";

        channel.QueueDeclare(deadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(deadLetterQueue, deadLetterExchange, routingKey);

        var queueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = deadLetterExchange,
            ["x-dead-letter-routing-key"] = routingKey
        };
        channel.QueueDeclare(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments);
        channel.QueueBind(queueName, RabbitMqEventPublisher.ExchangeName, routingKey);
    }
}
