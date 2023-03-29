namespace PhotoMap.Shared
{
    public interface IUserIdentifier
    {
        int UserId { get; set; }
        string GetKey();
    }
}
