using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Services.Storage;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IProductImageStorage
{
    Task<ProductImageStorageResult> SaveAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        CancellationToken cancellationToken = default);

    Task<ProductImageStorageResult> SaveAdditionalAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        int existingImageCount,
        CancellationToken cancellationToken = default);

    Task DeleteFilesAsync(
        IReadOnlyList<CreateProductImageRequest> images,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string folderName,
        CancellationToken cancellationToken = default);
}
