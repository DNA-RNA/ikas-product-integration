using System.Net;

namespace MultiSiteIkas.Core.Exceptions;

public sealed class IkasApiException : Exception
{
    public string StoreCode { get; }
    public HttpStatusCode StatusCode { get; }

    public IkasApiException(string storeCode, HttpStatusCode statusCode, string message)
        : base($"[{storeCode}] HTTP {(int)statusCode}: {message}")
    {
        StoreCode = storeCode;
        StatusCode = statusCode;
    }
}
