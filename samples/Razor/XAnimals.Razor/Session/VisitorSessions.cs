using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace XAnimals.Razor.Session;

/// <summary>
/// What a visitor keeps between two requests: a service scope, so the routers and the view models of the visitor stay alive
/// (history, signed-in state, pets adopted, dialogs waiting for an answer). It lives in the memory of the process.
/// </summary>
public sealed class VisitorSession(string id, IServiceScope scope) : IDisposable
{
    public string Id { get; } = id;

    public IServiceScope Scope { get; } = scope;

    /// <summary>One request at a time per visitor: the routers and the view models are not made for two requests at once.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public void Dispose()
    {
        // The view models that listen or count down stop with their scope (HistoryViewModel, FeaturesViewModel...).
        Scope.Dispose();
        Gate.Dispose();
    }
}

/// <summary>The sessions of the visitors, found by the cookie of the request. Idle sessions are dropped.</summary>
public sealed class VisitorSessions(IServiceScopeFactory scopes) : IDisposable
{
    /// <summary>The name of the cookie that holds the identifier of the session.</summary>
    public const string CookieName = "xanimals.visitor";

    /// <summary>How long a visitor can stay away before the session is dropped.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(20);

    /// <summary>The sessions the process keeps; the least recently seen one leaves when the limit is reached.</summary>
    public const int MaxSessions = 1000;

    private readonly ConcurrentDictionary<string, VisitorSession> _sessions = new();

    public int Count => _sessions.Count;

    /// <summary>The session of <paramref name="id"/>, or a new one when the cookie is missing, unknown or expired.</summary>
    public VisitorSession GetOrCreate(string? id, out bool created)
    {
        Sweep();

        if (id is not null && _sessions.TryGetValue(id, out var existing))
        {
            created = false;
            return existing;
        }

        created = true;
        var session = new VisitorSession(Guid.NewGuid().ToString("N"), scopes.CreateScope());
        _sessions[session.Id] = session;
        return session;
    }

    private void Sweep()
    {
        var limit = DateTime.UtcNow - IdleTimeout;
        foreach (var (id, session) in _sessions)
            if (session.LastSeen < limit)
                Remove(id);

        // The oldest ones leave when there are too many.
        while (_sessions.Count > MaxSessions && _sessions.MinBy(s => s.Value.LastSeen).Key is { } oldest)
            Remove(oldest);
    }

    private void Remove(string id)
    {
        if (!_sessions.TryRemove(id, out var session))
            return;

        // A request that holds the session finishes first; nobody is left to use it after that.
        _ = Task.Run(async () =>
        {
            await session.Gate.WaitAsync();
            session.Dispose();
        });
    }

    public void Dispose()
    {
        foreach (var id in _sessions.Keys)
            if (_sessions.TryRemove(id, out var session))
                session.Dispose();
    }
}

/// <summary>Gives each request the services of the session of its visitor.</summary>
internal sealed class VisitorSessionMiddleware(RequestDelegate next, VisitorSessions sessions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var session = sessions.GetOrCreate(context.Request.Cookies[VisitorSessions.CookieName], out var created);
        if (created)
        {
            context.Response.Cookies.Append(VisitorSessions.CookieName, session.Id, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps
            });
        }

        await session.Gate.WaitAsync(context.RequestAborted);
        var requestServices = context.RequestServices;
        try
        {
            session.LastSeen = DateTime.UtcNow;
            context.RequestServices = session.Scope.ServiceProvider;
            await next(context);
        }
        finally
        {
            context.RequestServices = requestServices;
            session.LastSeen = DateTime.UtcNow;
            session.Gate.Release();
        }
    }
}

public static class VisitorSessionExtensions
{
    /// <summary>Keeps one scope per visitor in memory: <c>@inject IRouter</c> in a page is the router of the visitor, not of the request.</summary>
    public static IServiceCollection AddVisitorSessions(this IServiceCollection services)
        => services.AddSingleton<VisitorSessions>();

    /// <summary>Call it after <c>UseStaticFiles()</c>: the files do not need a session.</summary>
    public static IApplicationBuilder UseVisitorSessions(this IApplicationBuilder app)
        => app.UseMiddleware<VisitorSessionMiddleware>();
}
