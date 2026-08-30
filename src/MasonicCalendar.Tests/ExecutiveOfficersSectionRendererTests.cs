namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Services.Renderers.SectionRenderers;
using System.Text;
using System.Text.RegularExpressions;

public class ExecutiveOfficersSectionRendererTests
{
    [Fact]
    public void PbqDataSource_LoadsDeputyHeading()
    {
        var documentRoot = Path.Combine(FindRepositoryRoot(), "document");
        var loader = new DocumentLayoutLoader(documentRoot);

        var result = loader.LoadDataSourceMapping("data_sources/pbq_data_source.yaml");

        Assert.True(result.Success, result.Error);
        Assert.Equal("South Western Counties Region", result.Data?.OrderExecutiveOfficers?.DeputyHeading);
    }

    [Fact]
    public async Task PbqExecutiveOfficers_RendersConfiguredDeputyHeadingOnce()
    {
        var templateRoot = Path.Combine(FindRepositoryRoot(), "document", "templates");
        var renderer = new ExecutiveOfficersSectionRenderer(templateRoot, null, false);
        var section = new SectionConfig
        {
            SectionId = "pbq_executive_officers",
            SectionTitle = "The Worshipful Society of Free Masons",
            Template = "_data-driven/list-executive-officers.html",
            DataMapping = "data_sources/pbq_data_source.yaml"
        };
        var output = new StringBuilder();

        await renderer.RenderAsync(section, 0, [section], "master_v1", [], output);

        var html = output.ToString();
        Assert.Single(Regex.Matches(html, ">South Western Counties Region<"));
        Assert.True(
            html.IndexOf("South Western Counties Region", StringComparison.Ordinal) <
            html.IndexOf("2nd Grand Master Mason", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MasonicCalendar.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}