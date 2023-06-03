namespace PhotoMap.Api.Services;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}