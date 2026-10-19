using LegacyLift.Domain;

namespace LegacyLift.Application.Products;

// Puerto de mensajería. La implementación con RabbitMQ vive en Infrastructure.
public interface IProductEvents
{
    Task PublishCreatedAsync(Product product);
}
