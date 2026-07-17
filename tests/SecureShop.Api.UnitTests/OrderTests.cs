using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Domain.Enums;

namespace SecureShop.Api.UnitTests;

public sealed class OrderTests
{
    [Fact]
    public void AddItem_CalculatesTrustedTotal()
    {
        var order = CreateOrder();

        order.AddItem(
            Guid.NewGuid(),
            "Product",
            "SKU-1",
            12.50m,
            3);

        Assert.Equal(37.50m, order.TotalAmount);
        Assert.Single(order.Items);
    }

    [Fact]
    public void StatusFlow_RequiresExpectedOrder()
    {
        var order = CreateOrder();
        order.AddItem(
            Guid.NewGuid(),
            "Product",
            "SKU-1",
            10m,
            1);
        var employeeId = Guid.NewGuid();

        order.Approve(employeeId);
        order.MarkReadyForPickup(employeeId);
        order.Complete(employeeId);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedAtUtc);
    }

    [Fact]
    public void Complete_RejectsOrderThatIsNotReady()
    {
        var order = CreateOrder();

        Assert.Throws<InvalidOperationException>(
            () => order.Complete(Guid.NewGuid()));
    }

    [Fact]
    public void Cancel_RejectsReadyOrder()
    {
        var order = CreateOrder();
        var employeeId = Guid.NewGuid();
        order.Approve(employeeId);
        order.MarkReadyForPickup(employeeId);

        Assert.Throws<InvalidOperationException>(
            () => order.Cancel(employeeId));
    }

    private static Order CreateOrder() =>
        new(
            Guid.NewGuid(),
            "SSH-20260717-ABC12345",
            "Mustafa Özdemir",
            "Teststraße 1",
            "10115",
            "Berlin",
            "Almanya");
}
