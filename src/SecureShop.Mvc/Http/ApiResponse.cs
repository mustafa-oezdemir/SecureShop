using System.Net;

namespace SecureShop.Mvc.Http;

public sealed record ApiResponse<T>(
    bool IsSuccess,
    HttpStatusCode StatusCode,
    T? Data,
    string? ErrorMessage)
{
    public static ApiResponse<T> Success(
        HttpStatusCode statusCode,
        T data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new ApiResponse<T>(
            IsSuccess: true,
            StatusCode: statusCode,
            Data: data,
            ErrorMessage: null);
    }

    public static ApiResponse<T> Failure(
        HttpStatusCode statusCode,
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ApiResponse<T>(
            IsSuccess: false,
            StatusCode: statusCode,
            Data: default,
            ErrorMessage: errorMessage);
    }
}
