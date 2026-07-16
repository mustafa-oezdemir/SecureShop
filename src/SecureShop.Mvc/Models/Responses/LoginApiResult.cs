using System.Net;
namespace SecureShop.Mvc.Models.Responses;
public sealed record LoginApiResult(bool Succeeded,HttpStatusCode StatusCode,string? AuthenticationCookie,string? ErrorMessage);
