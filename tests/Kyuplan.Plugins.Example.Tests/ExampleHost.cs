using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kyuplan.Plugins.Example.Tests;

/// <summary>What Kyuplan gives <see cref="IKyuplanPlugin.Initialize"/>, recording what the plugin registers.</summary>
internal sealed class FakeBuilder : IPluginBuilder
{
    public Dictionary<string, Func<PluginDataSourceContext, IDataSource>> DataSources { get; } = [];

    public List<Action<RouteGroupBuilder>> Endpoints { get; } = [];

    public string Id => ExamplePlugin.PluginId;

    public string Version => "1.2.3";

    public string Directory => AppContext.BaseDirectory;

    public ILoggerFactory Loggers => NullLoggerFactory.Instance;

    public void AddDataSource(string id, Func<PluginDataSourceContext, IDataSource> create) => DataSources.Add(id, create);

    public void MapEndpoints(Action<RouteGroupBuilder> map) => Endpoints.Add(map);

    public string Permission(string name) => $"{Id}.{name}";
}

/// <summary>
/// Signs a request in from headers, so the tests need no accounts: <c>X-Test-User</c> is the name of a signed-in user and <c>X-Test-Permissions:
/// example.view,example.edit</c> the permissions of that user (as the claims Kyuplan puts on a signed-in user). A request with neither is nobody (401).
/// </summary>
internal sealed class HeaderAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    public const string PermissionsHeader = "X-Test-Permissions";

    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var name)) return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.Name, name.ToString()) };
        var permissions = Request.Headers.TryGetValue(PermissionsHeader, out var header) ? header.ToString() : "";
        claims.AddRange(permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => new Claim(PluginEndpointExtensions.PermissionClaimType, p)));

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// The endpoints of the plugin in a running (in-memory) web server, mapped the way Kyuplan maps them: a group <c>/api/plugins/example</c> that needs a login.
/// Use it to test endpoints and permissions end to end.
/// </summary>
internal sealed class ExampleHost : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplication _app;

    private ExampleHost(WebApplication app, FakeTimeProvider time)
    {
        _app = app;
        Time = time;
    }

    public FakeTimeProvider Time { get; }

    /// <summary>Every endpoint that is mapped, with the metadata (methods, authorization) it was given.</summary>
    public IEnumerable<Endpoint> Endpoints => ((IEndpointRouteBuilder)_app).DataSources.SelectMany(source => source.Endpoints);

    public static async Task<ExampleHost> StartAsync(params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value));

        var time = new FakeTimeProvider(Now);
        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddAuthentication(HeaderAuthentication.SchemeName).AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>(HeaderAuthentication.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        var plugin = new FakeBuilder();
        new ExamplePlugin().Initialize(plugin);
        var group = app.MapGroup($"/api/plugins/{plugin.Id}").RequireAuthorization();
        foreach (var map in plugin.Endpoints) map(group);

        await app.StartAsync();
        return new ExampleHost(app, time);
    }

    /// <summary>A client that is not signed in.</summary>
    public HttpClient Anonymous() => _app.GetTestClient();

    /// <summary>A client signed in as a user who has these permissions (full ids: <c>example.view</c>).</summary>
    public HttpClient SignedIn(string user = "tester", params string[] permissions)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderAuthentication.UserHeader, user);
        if (permissions.Length > 0) client.DefaultRequestHeaders.Add(HeaderAuthentication.PermissionsHeader, string.Join(',', permissions));
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
