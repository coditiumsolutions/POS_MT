namespace POS_MT.Interfaces;

public interface IPasswordHashService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);

    bool NeedsRehash(string passwordHash);
}
