namespace MultiSiteIkas.Core.Exceptions;

public sealed class XmlValidationException : Exception
{
    public XmlValidationException(string message) : base(message) { }
    public XmlValidationException(string message, Exception inner) : base(message, inner) { }
}
