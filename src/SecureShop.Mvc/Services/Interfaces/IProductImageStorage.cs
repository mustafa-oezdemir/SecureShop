using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Services.Storage;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IProductImageStorage
{
    Task<ProductImageStorageResult> SaveAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string folderName,
        CancellationToken cancellationToken = default);
}
