using POS_MT.Services;

namespace POS_MT.Interfaces;

public interface INavContextService
{
    string? CurrentAreaKey { get; }

    TopNavArea? CurrentArea { get; }

    void SetArea(string? areaKey);

    void Clear();
}
