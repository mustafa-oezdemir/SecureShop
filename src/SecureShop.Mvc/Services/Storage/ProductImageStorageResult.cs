using SecureShop.Mvc.Models.Requests;

namespace SecureShop.Mvc.Services.Storage;

public sealed record ProductImageStorageResult(
    bool Succeeded,
    string FolderName,
    IReadOnlyList<CreateProductImageRequest> Images,
    string? ErrorMessage)
{
    public static ProductImageStorageResult Success(
        string folderName,
        IReadOnlyList<CreateProductImageRequest> images) =>
        new(true, folderName, images, null);

    public static ProductImageStorageResult Failure(
        string errorMessage) =>
        new(false, string.Empty, [], errorMessage);
}
