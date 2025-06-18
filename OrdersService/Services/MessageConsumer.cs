using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using OrdersService.Data;
using System.Text;

namespace OrdersService.Services;

public class MessageConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<MessageConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public MessageConsumer(IServiceProvider serviceProvider, ConnectionFactory connectionFactory, ILogger<MessageConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeRabbitMQAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task InitializeRabbitMQAsync(CancellationToken cancellationToken)
    {
        try
        {
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            
            await _channel.ExchangeDeclareAsync("payments", ExchangeType.Topic, true, cancellationToken: cancellationToken);
            
            var queueResult = await _channel.QueueDeclareAsync("orders-payment-events", true, false, false, cancellationToken: cancellationToken);
            
            await _channel.QueueBindAsync(queueResult.QueueName, "payments", "PaymentProcessedEvent", cancellationToken: cancellationToken);
            
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += OnMessageReceivedAsync;

            await _channel.BasicConsumeAsync(queueResult.QueueName, false, consumer, cancellationToken);

            _logger.LogInformation("RabbitMQ consumer initialized and listening for payment events");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ consumer");
        }
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs e)
    {
        try
        {
            var body = e.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            
            var inboxMessage = new InboxMessage
            {
                Id = Guid.NewGuid(),
                Type = e.RoutingKey,
                Content = message,
                OccurredOn = DateTime.UtcNow
            };

            context.InboxMessages.Add(inboxMessage);
            await context.SaveChangesAsync();
            
            if (_channel != null)
            {
                await _channel.BasicAckAsync(e.DeliveryTag, false);
            }

            _logger.LogInformation("Received and stored message of type {MessageType}", e.RoutingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process received message");
            
            if (_channel != null)
            {
                await _channel.BasicNackAsync(e.DeliveryTag, false, true);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}
