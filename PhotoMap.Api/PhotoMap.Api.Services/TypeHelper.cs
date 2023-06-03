namespace PhotoMap.Api.Services;

public static class TypeHelper
{
    public static string GetTypeFullName(Type type)
    {
        return $"{type.FullName}, {type.Assembly.FullName}";
    }
}