namespace PhotoMap.Api.Domain.Models;

public class ComplexResponse<T>
{
    public List<T> Values { get; set; }
    public int TotalCount { get; set; }
}
