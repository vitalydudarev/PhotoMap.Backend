namespace PhotoMap.Shared
{
    public interface IUserIdentifier
    {
        long UserId { get; set; }
        string GetKey();
    }
}
