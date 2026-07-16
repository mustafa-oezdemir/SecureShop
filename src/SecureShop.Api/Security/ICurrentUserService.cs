namespace SecureShop.Api.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
