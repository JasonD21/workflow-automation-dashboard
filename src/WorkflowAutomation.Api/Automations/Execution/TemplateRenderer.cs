using System.Text.RegularExpressions;

namespace WorkflowAutomation.Api.Automations.Execution;

public interface ITemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, string> tokens);
}

public partial class TemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([\w.]+)\s*\}\}")]
    private static partial Regex TokenPattern();

    public string Render(string template, IReadOnlyDictionary<string, string> tokens) => TokenPattern().Replace(template, m =>
    {
        var key = m.Groups[1].Value;
        return tokens.TryGetValue(key, out var v) ? v : m.Value;
    });

}