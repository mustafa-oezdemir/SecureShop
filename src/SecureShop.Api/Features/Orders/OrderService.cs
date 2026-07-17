using System.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Domain.Enums;
using SecureShop.Api.Features.Audit;
using SecureShop.Api.Features.QrCodes;

namespace SecureShop.Api.Features.Orders;

public sealed class OrderService : IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _audit;
    private readonly IOrderQrTokenService _qrTokenService;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly OrderQrOptions _qrOptions;

    public OrderService(
        AppDbContext dbContext,
        IAuditService audit,
        IOrderQrTokenService qrTokenService,
        IQrCodeGenerator qrCodeGenerator,
        IOptions<OrderQrOptions> qrOptions)
    {
        _dbContext = dbContext;
        _audit = audit;
        _qrTokenService = qrTokenService;
        _qrCodeGenerator = qrCodeGenerator;
        _qrOptions = qrOptions.Value;
    }

    public async Task<OrderMutationResult> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;

        if (_dbContext.Database.IsRelational())
        {
            transaction = await _dbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
        }

        await using (transaction)
        {
            var cart = await _dbContext.Carts
                .Include(currentCart => currentCart.Items)
                    .ThenInclude(item => item.Product)
                        .ThenInclude(product => product.Category)
                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    currentCart => currentCart.UserId == userId,
                    cancellationToken);

            if (cart is null || cart.Items.Count == 0)
            {
                return new(OrderMutationStatus.CartEmpty);
            }

            foreach (var item in cart.Items)
            {
                if (!item.Product.IsActive
                    || !item.Product.Category.IsActive)
                {
                    return new(
                        OrderMutationStatus.ProductUnavailable);
                }

                if (item.Quantity > item.Product.StockQuantity)
                {
                    return new(
                        OrderMutationStatus.InsufficientStock);
                }
            }

            var order = new Order(
                userId,
                CreateOrderNumber(),
                request.RecipientName,
                request.AddressLine,
                request.PostalCode,
                request.City,
                request.Country);

            foreach (var item in cart.Items)
            {
                order.AddItem(
                    item.ProductId,
                    item.Product.Name,
                    item.Product.Sku,
                    item.Product.Price,
                    item.Quantity);

                item.Product.DecreaseStock(item.Quantity);
            }

            cart.Clear();
            _dbContext.Orders.Add(order);

            _audit.Record(
                "Order.Created",
                nameof(Order),
                order.Id.ToString("D"),
                new
                {
                    order.OrderNumber,
                    order.TotalAmount,
                    ItemCount = order.Items.Count
                });

            try
            {
                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(
                        cancellationToken);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);
                }

                return new(
                    OrderMutationStatus.ConcurrencyConflict);
            }
            catch (DbUpdateException)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);
                }

                return new(
                    OrderMutationStatus.ConcurrencyConflict);
            }

            var createdOrder = await GetCustomerOrderAsync(
                userId,
                order.OrderNumber,
                cancellationToken);

            return new(
                OrderMutationStatus.Succeeded,
                createdOrder);
        }
    }

    public async Task<IReadOnlyList<OrderResponse>>
        GetCustomerOrdersAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        var orders = await BaseOrderQuery(tracking: false)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => Map(order, includeQr: false))
            .ToList();
    }

    public async Task<OrderResponse?> GetCustomerOrderAsync(
        Guid userId,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var normalizedOrderNumber =
            NormalizeOrderNumber(orderNumber);

        var order = await BaseOrderQuery(tracking: false)
            .SingleOrDefaultAsync(
                currentOrder =>
                    currentOrder.UserId == userId
                    && currentOrder.OrderNumber
                        == normalizedOrderNumber,
                cancellationToken);

        return order is null
            ? null
            : Map(order, includeQr: true);
    }

    public async Task<IReadOnlyList<OrderResponse>>
        GetStaffOrdersAsync(
            CancellationToken cancellationToken)
    {
        var orders = await BaseOrderQuery(tracking: false)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => Map(order, includeQr: false))
            .ToList();
    }

    public async Task<OrderResponse?> GetStaffOrderAsync(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var normalizedOrderNumber =
            NormalizeOrderNumber(orderNumber);

        var order = await BaseOrderQuery(tracking: false)
            .SingleOrDefaultAsync(
                currentOrder => currentOrder.OrderNumber
                    == normalizedOrderNumber,
                cancellationToken);

        return order is null
            ? null
            : Map(order, includeQr: true);
    }

    public Task<OrderMutationResult> ApproveAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            staffUserId,
            rowVersion,
            "Order.Approved",
            order => order.Approve(staffUserId),
            restoreStock: false,
            cancellationToken);

    public Task<OrderMutationResult> MarkReadyAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            staffUserId,
            rowVersion,
            "Order.ReadyForPickup",
            order => order.MarkReadyForPickup(staffUserId),
            restoreStock: false,
            cancellationToken);

    public Task<OrderMutationResult> CancelAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            staffUserId,
            rowVersion,
            "Order.Cancelled",
            order => order.Cancel(staffUserId),
            restoreStock: true,
            cancellationToken);

    public async Task<OrderMutationResult> CompleteByQrAsync(
        string token,
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        if (!_qrTokenService.TryValidate(token, out var orderId))
        {
            return new(OrderMutationStatus.InvalidQrCode);
        }

        var order = await BaseOrderQuery(tracking: true)
            .SingleOrDefaultAsync(
                currentOrder => currentOrder.Id == orderId,
                cancellationToken);

        if (order is null)
        {
            return new(OrderMutationStatus.NotFound);
        }

        try
        {
            order.Complete(staffUserId);
        }
        catch (InvalidOperationException)
        {
            return new(OrderMutationStatus.InvalidTransition);
        }

        _audit.Record(
            "Order.CompletedByQr",
            nameof(Order),
            order.Id.ToString("D"),
            new
            {
                order.OrderNumber
            });

        return await SaveProcessedOrderAsync(
            order,
            cancellationToken);
    }

    private async Task<OrderMutationResult> ProcessAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        string auditAction,
        Action<Order> transition,
        bool restoreStock,
        CancellationToken cancellationToken)
    {
        var normalizedOrderNumber =
            NormalizeOrderNumber(orderNumber);

        var order = await BaseOrderQuery(tracking: true)
            .SingleOrDefaultAsync(
                currentOrder => currentOrder.OrderNumber
                    == normalizedOrderNumber,
                cancellationToken);

        if (order is null)
        {
            return new(OrderMutationStatus.NotFound);
        }

        if (!TryDecodeRowVersion(rowVersion, out var version))
        {
            return new(OrderMutationStatus.InvalidRowVersion);
        }

        _dbContext.Entry(order)
            .Property(currentOrder => currentOrder.RowVersion)
            .OriginalValue = version;

        try
        {
            transition(order);
        }
        catch (InvalidOperationException)
        {
            return new(OrderMutationStatus.InvalidTransition);
        }

        if (restoreStock)
        {
            foreach (var item in order.Items)
            {
                item.Product.IncreaseStock(item.Quantity);
            }
        }

        _audit.Record(
            auditAction,
            nameof(Order),
            order.Id.ToString("D"),
            new
            {
                order.OrderNumber,
                Status = order.Status.ToString()
            });

        return await SaveProcessedOrderAsync(
            order,
            cancellationToken);
    }

    private async Task<OrderMutationResult> SaveProcessedOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(OrderMutationStatus.ConcurrencyConflict);
        }

        var response = await GetStaffOrderAsync(
            order.OrderNumber,
            cancellationToken);

        return new(
            OrderMutationStatus.Succeeded,
            response);
    }

    private IQueryable<Order> BaseOrderQuery(bool tracking)
    {
        var query = _dbContext.Orders
            .Include(order => order.Items)
                .ThenInclude(item => item.Product)
            .AsSplitQuery();

        return tracking ? query : query.AsNoTracking();
    }

    private OrderResponse Map(
        Order order,
        bool includeQr)
    {
        string? qrCodeDataUrl = null;

        if (includeQr
            && order.Status is OrderStatus.Approved
                or OrderStatus.ReadyForPickup)
        {
            var token = _qrTokenService.Generate(order.Id);
            var verificationUrl = QueryHelpers.AddQueryString(
                _qrOptions.VerificationBaseUrl,
                "token",
                token);

            qrCodeDataUrl = _qrCodeGenerator.GeneratePngDataUrl(
                verificationUrl);
        }

        return new OrderResponse(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.RecipientName,
            order.AddressLine,
            order.PostalCode,
            order.City,
            order.Country,
            order.Status.ToString(),
            order.TotalAmount,
            order.Items
                .OrderBy(item => item.ProductName)
                .Select(item => new OrderItemResponse(
                    item.ProductId,
                    item.ProductName,
                    item.Sku,
                    item.UnitPrice,
                    item.Quantity,
                    item.LineTotal))
                .ToList(),
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            order.CompletedAtUtc,
            Convert.ToBase64String(order.RowVersion),
            qrCodeDataUrl);
    }

    private static string CreateOrderNumber() =>
        $"SSH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21]
            .ToUpperInvariant();

    private static string NormalizeOrderNumber(
        string orderNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        return orderNumber.Trim().ToUpperInvariant();
    }

    private static bool TryDecodeRowVersion(
        string value,
        out byte[] rowVersion)
    {
        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length == 8;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }
}
