using POS_MT.Interfaces;

namespace POS_MT.Services;

public sealed class NavContextService : INavContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public NavContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CurrentAreaKey
    {
        get
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            return session?.GetString(NavigationCatalog.SessionKey);
        }
    }

    public TopNavArea? CurrentArea => NavigationCatalog.FindArea(CurrentAreaKey);

    public void SetArea(string? areaKey)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return;
        }

        var area = NavigationCatalog.FindArea(areaKey);
        if (area is null)
        {
            session.Remove(NavigationCatalog.SessionKey);
            return;
        }

        session.SetString(NavigationCatalog.SessionKey, area.Key);
    }

    public void Clear()
    {
        _httpContextAccessor.HttpContext?.Session.Remove(NavigationCatalog.SessionKey);
    }
}
