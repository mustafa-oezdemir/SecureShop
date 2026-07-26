# 1. Bu Adımın Amacı

Domain iş kurallarını, sipariş servis mutasyonlarını, süreli QR token güvenliğini, gerçek API pipeline'ını, authorization/güvenlik başlıklarını, MVC typed HttpClient payload'larını ve görsel dosya doğrulamasını tekrarlanabilir otomatik testlerle güvenceye almak.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
Test projeleri
```

# 3. Bu Adımda Yapılacaklar

1. API unit test projesini solution'a eklemek.
2. API integration test projesini WebApplicationFactory ile kurmak.
3. MVC servis/storage test projesini eklemek.
4. Product ve Order domain kurallarını test etmek.
5. Checkout'un sunucu fiyatı kullandığını ve atomik davrandığını test etmek.
6. QR round-trip ve tamper reddini test etmek.
7. Public product endpoint'leri, 401 ve security header'ları test etmek.
8. MVC'nin güvenilmeyen userId/fiyat/toplam göndermediğini test etmek.
9. Çoklu görsel storage signature doğrulamasını test etmek.

# 4. CLI Komutları

Çalışma dizini: ``D:\Code\ASP.NET\SecureShop``

```powershell
dotnet restore SecureShop.sln
dotnet build SecureShop.sln -c Release --no-restore
dotnet test SecureShop.sln -c Release --no-build
```

Veritabanı modeli içeren adımlarda:

```powershell
dotnet ef database update --project src\SecureShop.Api\SecureShop.Api.csproj --startup-project src\SecureShop.Api\SecureShop.Api.csproj --configuration Release
dotnet ef migrations has-pending-model-changes --project src\SecureShop.Api\SecureShop.Api.csproj --startup-project src\SecureShop.Api\SecureShop.Api.csproj --configuration Release
```

Terminal 1 — çalışma dizini: ``src\SecureShop.Api``

```powershell
dotnet watch run --launch-profile https
```

Terminal 2 — çalışma dizini: ``src\SecureShop.Mvc``

```powershell
dotnet watch run --launch-profile https
```

Çalışan eski ``dotnet watch`` süreci EXE dosyasını kilitlerse önce ilgili terminalde
``Ctrl+C`` kullanılmalıdır. Ardından iki uygulama yeniden başlatılmalıdır.

# 5. Güncel Proje Yapısı

```text
tests/
├── SecureShop.Api.UnitTests
├── SecureShop.Api.IntegrationTests
└── SecureShop.Mvc.Tests
```

# 6. Dosya Bazında Eksiksiz Kodlar

Bu bölümdeki içerikler tamamlanmış çalışma ağacındaki dosyaların eksiksiz güncel
halleridir; ``...`` ile kısaltma yapılmamıştır.

## `SecureShop.sln`

Uzantı: `.sln`

``text

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{827E0CD3-B72D-47B6-A68D-7590B98EB39B}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SecureShop.Api", "src\SecureShop.Api\SecureShop.Api.csproj", "{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SecureShop.Mvc", "src\SecureShop.Mvc\SecureShop.Mvc.csproj", "{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{0AB3BF05-4346-4AA6-1389-037BE0695223}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SecureShop.Api.UnitTests", "tests\SecureShop.Api.UnitTests\SecureShop.Api.UnitTests.csproj", "{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SecureShop.Api.IntegrationTests", "tests\SecureShop.Api.IntegrationTests\SecureShop.Api.IntegrationTests.csproj", "{46965027-3612-43C7-898A-6CAE06010B48}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SecureShop.Mvc.Tests", "tests\SecureShop.Mvc.Tests\SecureShop.Mvc.Tests.csproj", "{E1BAFB57-254E-4A06-860F-52FFAE630176}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Debug|x64.ActiveCfg = Debug|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Debug|x64.Build.0 = Debug|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Debug|x86.ActiveCfg = Debug|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Debug|x86.Build.0 = Debug|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Release|Any CPU.Build.0 = Release|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Release|x64.ActiveCfg = Release|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Release|x64.Build.0 = Release|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Release|x86.ActiveCfg = Release|Any CPU
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4}.Release|x86.Build.0 = Release|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Debug|x64.ActiveCfg = Debug|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Debug|x64.Build.0 = Debug|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Debug|x86.ActiveCfg = Debug|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Debug|x86.Build.0 = Debug|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Release|Any CPU.Build.0 = Release|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Release|x64.ActiveCfg = Release|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Release|x64.Build.0 = Release|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Release|x86.ActiveCfg = Release|Any CPU
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7}.Release|x86.Build.0 = Release|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Debug|x64.ActiveCfg = Debug|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Debug|x64.Build.0 = Debug|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Debug|x86.ActiveCfg = Debug|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Debug|x86.Build.0 = Debug|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Release|Any CPU.Build.0 = Release|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Release|x64.ActiveCfg = Release|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Release|x64.Build.0 = Release|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Release|x86.ActiveCfg = Release|Any CPU
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB}.Release|x86.Build.0 = Release|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Debug|x64.ActiveCfg = Debug|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Debug|x64.Build.0 = Debug|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Debug|x86.ActiveCfg = Debug|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Debug|x86.Build.0 = Debug|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Release|Any CPU.Build.0 = Release|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Release|x64.ActiveCfg = Release|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Release|x64.Build.0 = Release|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Release|x86.ActiveCfg = Release|Any CPU
		{46965027-3612-43C7-898A-6CAE06010B48}.Release|x86.Build.0 = Release|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Debug|x64.ActiveCfg = Debug|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Debug|x64.Build.0 = Debug|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Debug|x86.ActiveCfg = Debug|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Debug|x86.Build.0 = Debug|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Release|Any CPU.Build.0 = Release|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Release|x64.ActiveCfg = Release|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Release|x64.Build.0 = Release|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Release|x86.ActiveCfg = Release|Any CPU
		{E1BAFB57-254E-4A06-860F-52FFAE630176}.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{B8BFD2CD-9D6C-4E21-A0D5-C2D05D518AE4} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{23EA5CC8-5D54-4ED3-BECC-509C72CF01E7} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{5AA1CB5B-75CA-4E81-ADE8-DD807E9050CB} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{46965027-3612-43C7-898A-6CAE06010B48} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{E1BAFB57-254E-4A06-860F-52FFAE630176} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
	EndGlobalSection
EndGlobal
``

## `tests/SecureShop.Api.UnitTests/SecureShop.Api.UnitTests.csproj`

Uzantı: `.csproj`

``xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\SecureShop.Api\SecureShop.Api.csproj" />
  </ItemGroup>

</Project>
``

## `tests/SecureShop.Api.UnitTests/ProductTests.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.UnitTests;

public sealed class ProductTests
{
    [Fact]
    public void DecreaseStock_UsesServerSideStock()
    {
        var product = CreateProduct(stock: 10);

        product.DecreaseStock(3);

        Assert.Equal(7, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_RejectsInsufficientStock()
    {
        var product = CreateProduct(stock: 2);

        Assert.Throws<InvalidOperationException>(
            () => product.DecreaseStock(3));
    }

    [Fact]
    public void IncreaseStock_RestoresCancelledQuantity()
    {
        var product = CreateProduct(stock: 5);

        product.IncreaseStock(4);

        Assert.Equal(9, product.StockQuantity);
    }

    [Fact]
    public void AddImage_RejectsDuplicateSortOrder()
    {
        var product = CreateProduct(stock: 5);
        product.AddImage(
            "/images/products/TEST/1.png",
            "Test image",
            0,
            isPrimary: true);

        Assert.Throws<InvalidOperationException>(
            () => product.AddImage(
                "/images/products/TEST/2.png",
                "Second image",
                0));
    }

    private static Product CreateProduct(int stock) =>
        new(
            Guid.NewGuid(),
            "Test product",
            "TEST-SKU",
            19.99m,
            stock,
            "Test description");
}
``

## `tests/SecureShop.Api.UnitTests/OrderTests.cs`

Uzantı: `.cs`

``csharp
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
``

## `tests/SecureShop.Api.UnitTests/OrderServiceTests.cs`

Uzantı: `.cs`

``csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Features.Audit;
using SecureShop.Api.Features.Orders;
using SecureShop.Api.Features.QrCodes;

namespace SecureShop.Api.UnitTests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_UsesServerPriceAndClearsCart()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category("Test");
        var product = new Product(
            category.Id,
            "Server priced product",
            "SERVER-PRICE-01",
            49.90m,
            10,
            "Test product");
        var cart = new Cart(userId);

        cart.AddItem(product.Id, 2);
        dbContext.AddRange(category, product, cart);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.CreateAsync(
            userId,
            ValidRequest(),
            CancellationToken.None);

        Assert.Equal(
            OrderMutationStatus.Succeeded,
            result.Status);
        Assert.NotNull(result.Order);
        Assert.Equal(99.80m, result.Order.TotalAmount);
        Assert.Equal(8, product.StockQuantity);
        Assert.Empty(await dbContext.CartItems.ToListAsync());
        Assert.Single(await dbContext.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsInsufficientStockWithoutMutation()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category("Test");
        var product = new Product(
            category.Id,
            "Limited product",
            "LIMITED-01",
            10m,
            1,
            "Test product");
        var cart = new Cart(userId);

        cart.AddItem(product.Id, 2);
        dbContext.AddRange(category, product, cart);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.CreateAsync(
            userId,
            ValidRequest(),
            CancellationToken.None);

        Assert.Equal(
            OrderMutationStatus.InsufficientStock,
            result.Status);
        Assert.Equal(1, product.StockQuantity);
        Assert.Single(await dbContext.CartItems.ToListAsync());
        Assert.Empty(await dbContext.Orders.ToListAsync());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"SecureShop-OrderService-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static OrderService CreateService(
        AppDbContext dbContext) =>
        new(
            dbContext,
            new NullAuditService(),
            new StubQrTokenService(),
            new StubQrCodeGenerator(),
            Options.Create(new OrderQrOptions()));

    private static CreateOrderRequest ValidRequest() =>
        new()
        {
            RecipientName = "Test Customer",
            AddressLine = "Teststrasse 10",
            PostalCode = "10115",
            City = "Berlin",
            Country = "Deutschland"
        };

    private sealed class NullAuditService : IAuditService
    {
        public void Record(
            string action,
            string entityType,
            string? entityId,
            object? details = null)
        {
        }
    }

    private sealed class StubQrTokenService
        : IOrderQrTokenService
    {
        public string Generate(Guid orderId) =>
            orderId.ToString("N");

        public bool TryValidate(
            string token,
            out Guid orderId) =>
            Guid.TryParseExact(token, "N", out orderId);
    }

    private sealed class StubQrCodeGenerator : IQrCodeGenerator
    {
        public string GeneratePngDataUrl(string content) =>
            $"data:image/png;base64,{content}";
    }
}
``

## `tests/SecureShop.Api.UnitTests/OrderQrTokenServiceTests.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SecureShop.Api.Features.QrCodes;

namespace SecureShop.Api.UnitTests;

public sealed class OrderQrTokenServiceTests : IDisposable
{
    private readonly string _keyDirectory = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-qr-tests-{Guid.NewGuid():N}");

    [Fact]
    public void GenerateAndValidate_RoundTripsOrderId()
    {
        Directory.CreateDirectory(_keyDirectory);
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(_keyDirectory));
        var service = new OrderQrTokenService(
            provider,
            Options.Create(new OrderQrOptions
            {
                LifetimeMinutes = 30
            }));
        var orderId = Guid.NewGuid();

        var token = service.Generate(orderId);
        var isValid = service.TryValidate(
            token,
            out var validatedOrderId);

        Assert.True(isValid);
        Assert.Equal(orderId, validatedOrderId);
    }

    [Fact]
    public void TryValidate_RejectsTamperedToken()
    {
        Directory.CreateDirectory(_keyDirectory);
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(_keyDirectory));
        var service = new OrderQrTokenService(
            provider,
            Options.Create(new OrderQrOptions
            {
                LifetimeMinutes = 30
            }));

        var isValid = service.TryValidate(
            "tampered-token",
            out var orderId);

        Assert.False(isValid);
        Assert.Equal(Guid.Empty, orderId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyDirectory))
        {
            Directory.Delete(_keyDirectory, recursive: true);
        }
    }
}
``

## `tests/SecureShop.Api.IntegrationTests/SecureShop.Api.IntegrationTests.csproj`

Uzantı: `.csproj`

``xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.9" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\SecureShop.Api\SecureShop.Api.csproj" />
  </ItemGroup>

</Project>
``

## `tests/SecureShop.Api.IntegrationTests/SecureShopApiFactory.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.IntegrationTests;

public sealed class SecureShopApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _keyRingPath = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-api-tests-{Guid.NewGuid():N}");
    private readonly string _databaseName =
        $"SecureShop-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "Authentication:SharedCookie:KeyRingPath",
            _keyRingPath);
        builder.UseSetting(
            "QrCodes:Orders:VerificationBaseUrl",
            "https://localhost/employee/orders/verify");
        builder.UseSetting(
            "QrCodes:Orders:LifetimeMinutes",
            "30");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\MSSQLLocalDB;Database=UnusedTestDb;Trusted_Connection=True",
                    ["Authentication:Google:ClientId"] =
                        "integration-test-client",
                    ["Authentication:Google:ClientSecret"] =
                        "integration-test-secret",
                    ["Authentication:SharedCookie:KeyRingPath"] =
                        _keyRingPath,
                    ["QrCodes:Orders:VerificationBaseUrl"] =
                        "https://localhost/employee/orders/verify",
                    ["QrCodes:Orders:LifetimeMinutes"] = "30"
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(
                    _databaseName);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.EnsureCreated();

        var category = new Category("Elektronik");
        var product = new Product(
            category.Id,
            "Integration Product",
            "INTEGRATION-SKU",
            29.90m,
            12,
            "Integration test product");

        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.SaveChanges();

        return host;
    }

    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_keyRingPath))
        {
            Directory.Delete(_keyRingPath, recursive: true);
        }
    }
}
``

## `tests/SecureShop.Api.IntegrationTests/ProductEndpointTests.cs`

Uzantı: `.cs`

``csharp
using System.Net;
using System.Net.Http.Json;
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.IntegrationTests;

public sealed class ProductEndpointTests
    : IClassFixture<SecureShopApiFactory>
{
    private readonly SecureShopApiFactory _factory;

    public ProductEndpointTests(SecureShopApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProducts_ReturnsPublicCatalog()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/products");
        var products = await response.Content
            .ReadFromJsonAsync<List<ProductResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(products);
        Assert.Contains(
            products,
            product => product.Sku == "INTEGRATION-SKU");
    }

    [Fact]
    public async Task GetBySku_ReturnsExpectedProduct()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            "/api/products/by-sku/INTEGRATION-SKU");
        var product = await response.Content
            .ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("INTEGRATION-SKU", product?.Sku);
    }

    [Fact]
    public async Task UnknownSku_ReturnsNotFound()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            "/api/products/by-sku/DOES-NOT-EXIST");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
``

## `tests/SecureShop.Api.IntegrationTests/SecurityEndpointTests.cs`

Uzantı: `.cs`

``csharp
using System.Net;

namespace SecureShop.Api.IntegrationTests;

public sealed class SecurityEndpointTests
    : IClassFixture<SecureShopApiFactory>
{
    private readonly SecureShopApiFactory _factory;

    public SecurityEndpointTests(SecureShopApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cart_RequiresAuthentication()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/cart");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ApiResponse_ContainsSecurityHeaders()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/products");

        Assert.True(response.Headers.Contains(
            "X-Content-Type-Options"));
        Assert.True(response.Headers.Contains(
            "Content-Security-Policy"));
        Assert.True(response.Headers.Contains(
            "Referrer-Policy"));
    }
}
``

## `tests/SecureShop.Mvc.Tests/SecureShop.Mvc.Tests.csproj`

Uzantı: `.csproj`

``xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\SecureShop.Mvc\SecureShop.Mvc.csproj" />
  </ItemGroup>

</Project>
``

## `tests/SecureShop.Mvc.Tests/StubHttpMessageHandler.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Tests;

internal sealed class StubHttpMessageHandler
    : HttpMessageHandler
{
    private readonly Func<
        HttpRequestMessage,
        CancellationToken,
        Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _handler(request, cancellationToken);
}
``

## `tests/SecureShop.Mvc.Tests/ProductApiServiceTests.cs`

Uzantı: `.cs`

``csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Api;

namespace SecureShop.Mvc.Tests;

public sealed class ProductApiServiceTests
{
    [Fact]
    public async Task GetProductBySku_UsesEncodedSkuRoute()
    {
        Uri? requestedUri = null;
        var expected = new ProductResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Elektronik",
            "Product",
            "SKU-1",
            null,
            10m,
            2,
            true,
            [],
            DateTimeOffset.UtcNow,
            null,
            Convert.ToBase64String(new byte[8]));
        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                requestedUri = request.RequestUri;
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(expected)
                    });
            });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var service = new ProductApiService(
            client,
            NullLogger<ProductApiService>.Instance);

        var result = await service.GetProductBySkuAsync("SKU-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "/api/products/by-sku/SKU-1",
            requestedUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetProductBySku_MapsNotFound()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.NotFound)));
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var service = new ProductApiService(
            client,
            NullLogger<ProductApiService>.Instance);

        var result = await service.GetProductBySkuAsync(
            "MISSING");

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }
}
``

## `tests/SecureShop.Mvc.Tests/OrderApiServiceTests.cs`

Uzantı: `.cs`

``csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Api;

namespace SecureShop.Mvc.Tests;

public sealed class OrderApiServiceTests
{
    [Fact]
    public async Task Create_DoesNotSendUserPriceOrTotal()
    {
        string? requestJson = null;
        var responseModel = new OrderResponse(
            Guid.NewGuid(),
            "SSH-20260717-ABC12345",
            Guid.NewGuid(),
            "Customer",
            "Street 1",
            "10115",
            "Berlin",
            "Germany",
            "PendingApproval",
            29.90m,
            [],
            DateTimeOffset.UtcNow,
            null,
            null,
            Convert.ToBase64String(new byte[8]),
            null);
        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                requestJson = await request.Content!
                    .ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(
                    HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(responseModel)
                };
            });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var service = new OrderApiService(
            client,
            NullLogger<OrderApiService>.Instance);

        var result = await service.CreateAsync(
            new CreateOrderRequest(
                "Customer",
                "Street 1",
                "10115",
                "Berlin",
                "Germany"));

        Assert.True(result.IsSuccess);
        using var json = JsonDocument.Parse(requestJson!);
        Assert.False(json.RootElement.TryGetProperty(
            "userId",
            out _));
        Assert.False(json.RootElement.TryGetProperty(
            "totalAmount",
            out _));
        Assert.False(json.RootElement.TryGetProperty(
            "unitPrice",
            out _));
    }
}
``

## `tests/SecureShop.Mvc.Tests/ProductImageStorageTests.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SecureShop.Mvc.Services.Storage;

namespace SecureShop.Mvc.Tests;

public sealed class ProductImageStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-image-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_WritesValidatedPngInsideSkuFolder()
    {
        Directory.CreateDirectory(_root);
        var storage = new ProductImageStorage(
            new TestWebHostEnvironment(_root));
        await using var stream = new MemoryStream(
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        ]);
        var file = new FormFile(
            stream,
            0,
            stream.Length,
            "Images",
            "image.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await storage.SaveAsync(
            [file],
            "TEST-SKU",
            "Test Product");

        Assert.True(result.Succeeded);
        Assert.Single(result.Images);
        Assert.StartsWith(
            "/images/products/TEST-SKU/",
            result.Images[0].ImageUrl);
        Assert.Single(Directory.GetFiles(
            Path.Combine(
                _root,
                "images",
                "products",
                "TEST-SKU")));
    }

    [Fact]
    public async Task SaveAsync_RejectsFakePng()
    {
        Directory.CreateDirectory(_root);
        var storage = new ProductImageStorage(
            new TestWebHostEnvironment(_root));
        await using var stream = new MemoryStream(
            "not-a-png"u8.ToArray());
        var file = new FormFile(
            stream,
            0,
            stream.Length,
            "Images",
            "image.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await storage.SaveAsync(
            [file],
            "TEST-SKU",
            "Test Product");

        Assert.False(result.Succeeded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment
        : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
            ContentRootPath = webRootPath;
        }

        public string ApplicationName { get; set; } =
            "SecureShop.Mvc.Tests";
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; } =
            "Testing";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
``

# 7. Kod Açıklaması

Unit testler hızlı domain ve servis davranışlarını izole eder. Integration testler `WebApplicationFactory<Program>` ile gerçek middleware/controller pipeline'ını açar, geçici Data Protection key-ring ve benzersiz EF InMemory veritabanı kullanır. MVC testleri stub `HttpMessageHandler` ile URL, HTTP methodu ve JSON payload'ını doğrular. Görsel testleri geçerli PNG signature'ını kabul ederken sahte dosyayı reddeder ve geçici klasörü temizler.

# 8. API–MVC Veri Akışı

```text
dotnet test
    ↓
UnitTests → Domain + OrderService + QR token
    ↓
IntegrationTests → TestServer + API middleware/controllers
    ↓
Mvc.Tests → typed HttpClient + image storage
```

MVC yalnızca request/response modelleri ve typed ``HttpClient`` servisleri kullanır.
Kullanıcı kimliği, rol, fiyat, stok ve toplam tutar API authentication context'i ile
SQL Server verilerinden belirlenir.

# 9. Uygulama Sırası

1. Domain entity ve enum sınıflarını oluştur.
2. EF Core configuration ve ``AppDbContext`` değişikliklerini uygula.
3. API request/response contract'larını oluştur.
4. API servislerini ve controller endpoint'lerini ekle.
5. MVC request/response/ViewModel sınıflarını ekle.
6. Typed API servislerini ve MVC controller'larını ekle.
7. Razor View ve navbar bağlantılarını ekle.
8. Migration'ı uygula, build ve test komutlarını çalıştır.

# 10. Çalıştırma ve Test

API ve MVC yukarıdaki iki ayrı terminalde çalıştırılır. Ardından:

```text
API: https://localhost:7001
MVC: https://localhost:7002
```

Otomatik doğrulama:

```powershell
dotnet test SecureShop.sln -c Release
```

Bu uygulamada doğrulanan gerçek akış:

```text
Customer login → ürün → sepet → checkout → stok düşümü
Employee login → sipariş onayı → QR üretimi → iptal → stok iadesi
Admin login → audit log listesi
```

# 11. Beklenen Sonuç

Üç test assembly'sinde toplam 22 test geçer: API unit 12, API integration 5, MVC 5. Testler production SQL Server'ı değiştirmez; integration verisi benzersiz InMemory veritabanında, key-ring ve görsel dosyaları geçici klasörlerde tutulur.

# 12. Yaygın Hatalar

- Integration testinde SQL Server ve InMemory provider birlikte kalırsa `IDbContextOptionsConfiguration<AppDbContext>` kaydı kaldırılmalıdır.
- Her context için farklı InMemory adı üretilirse seed ve request farklı veritabanı görür; factory başına sabit ad kullanılmalıdır.
- Test key-ring klasörü yapılandırılmazsa production güvenlik kontrolü uygulama başlangıcını durdurur.
- Kilitli Debug EXE yerine test/build için Release configuration kullanılabilir.

- ``localhost:7001 connection refused``: API çalışmıyordur veya MVC
  ``ApiSettings:BaseUrl`` yanlış portu gösteriyordur.
- ``MSB3027/MSB3021 apphost.exe locked``: eski ``dotnet run/watch`` sürecini
  ``Ctrl+C`` ile kapatıp yeniden build alın.
- ``401``: authentication cookie yoktur veya ortak Data Protection key-ring ayarı
  iki uygulamada aynı değildir.
- ``403``: kullanıcı gerekli role/policy'ye sahip değildir.
- ``409``: sipariş durumu ya da ``RowVersion`` başka işlem tarafından değişmiştir;
  güncel detay yeniden yüklenmelidir.

# 13. Tamamlama Kontrol Listesi

```text
[x] CLI komutları doğru klasörde çalıştırıldı
[x] Dosyalar doğru projede oluşturuldu
[x] Web API hatasız derlendi
[x] MVC uygulaması hatasız derlendi
[x] MVC, Web API'ye başarıyla bağlandı
[x] MVC doğrudan veritabanına erişmiyor
[x] Web API veritabanı işlemini başarıyla gerçekleştirdi
[x] Migration uygulandı ve bekleyen model değişikliği yok
[x] Güvenlik ve rol kontrolleri doğrulandı
[x] Otomatik testlerin tamamı geçti
[x] İstenen özellik uçtan uca doğrulandı
```