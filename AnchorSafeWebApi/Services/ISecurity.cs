namespace AnchorSafe.API.Services
{
    public interface ISecurity
    {
        //string Pepper { get; set; }
        string GeneratePasswordHash(string pw);
        bool IsValid(string pwAttempt, string pwHash);
    }
}