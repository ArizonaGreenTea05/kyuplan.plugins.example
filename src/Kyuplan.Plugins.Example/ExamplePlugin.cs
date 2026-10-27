using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kyuplan.Plugins.Example;

/// <summary>
/// The entry point: the one public class of the assembly that implements <see cref="IKyuplanPlugin"/> (the file is named by <c>assembly</c> in <c>plugin.json</c>).
/// Kyuplan creates it once when the API starts and calls <see cref="Initialize"/>; what is registered there is what the plugin adds. Pages, permissions and the
/// settings form are not here: <c>plugin.json</c> and <c>ui/*.json</c> describe them, and the clients draw them.
/// </summary>
public sealed class ExamplePlugin : IKyuplanPlugin
{
    /// <summary>The id of the plugin, as <c>plugin.json</c> declares it. It starts the permissions (<c>example.view</c>) and the addresses (<c>/api/plugins/example/…</c>).</summary>
    public const string PluginId = "example";

    /// <summary>The id of the data source, as <c>plugin.json</c> declares it (<c>dataSources[].id</c>) and as the settings choose it (<c>DataSource:Kind</c>).</summary>
    public const string DataSourceId = "example";

    public void Initialize(IPluginBuilder builder)
    {
        // 1. A data source. The delegate runs when the API starts with this data source chosen, not before.
        builder.AddDataSource(DataSourceId, CreateDataSource);

        // 2. Endpoints below /api/plugins/example. The pages in ui/*.json read and write them. The state lives as long as the API runs (a change of the
        //    settings or of the plugins restarts it), so the notes of this example are gone then; keep yours in a database or a file of your own.
        var notes = NoteStore.WithSamples(TimeProvider.System.GetUtcNow());
        builder.MapEndpoints(routes => ExampleEndpoints.Map(routes, builder, notes));
    }

    private static IDataSource CreateDataSource(PluginDataSourceContext context)
    {
        // The settings of the data source: the section named by configSection in plugin.json (Plugins:example when it is left out). The form fills it,
        // and so can an administrator by hand (Plugins__example__MemberCount=20). A key that is not there leaves the default of the property.
        var options = context.Configuration.Get<ExampleOptions>() ?? new ExampleOptions();

        var log = context.Loggers.CreateLogger<ExampleDataSource>();
        // Say what is set, never the value of a secret.
        log.LogInformation("Example data source: {Members} members, training on {Day}, service {Service}.", options.MemberCount, options.TrainingDay, options.HasService ? "set" : "not set");

        return new ExampleDataSource(options, context.Time, context.ClubTimeZone, log);
    }
}
