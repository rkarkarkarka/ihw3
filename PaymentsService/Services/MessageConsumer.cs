using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace PaymentsService.Services;

public class MessageConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<MessageConsumer> _logger;

    public MessageConsumer(
        IServiceProvider serviceProvider, 
        ConnectionFactory connectionFactory,
        ILogger<MessageConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(exchange: "orders", type: ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
            
            var queueName = await channel.QueueDeclareAsync(
                queue: "order-events-payments", 
                durable: true, 
                exclusive: false, 
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: queueName.QueueName,
                exchange: "orders",
                routingKey: "OrderCreatedEvent",
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
                    
                    var inboxMessage = new InboxMessage
                    {
                        Id = Guid.NewGuid(),
                        Type = ea.RoutingKey,
                        Content = message,
                        OccurredOn = DateTime.UtcNow
                    };

                    context.InboxMessages.Add(inboxMessage);
                    await context.SaveChangesAsync();

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    
                    _logger.LogInformation("Processed message of type {MessageType}", ea.RoutingKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await channel.BasicConsumeAsync(
                queue: queueName.QueueName, 
                autoAck: false, 
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("MessageConsumer started successfully");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MessageConsumer");
            throw;
        }
    }
}
