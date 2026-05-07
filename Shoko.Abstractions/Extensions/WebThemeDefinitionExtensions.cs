using System;
using System.Linq;
using System.Text;
using Shoko.Abstractions.Web;

namespace Shoko.Abstractions.Extensions;

/// <summary>
///   Extension methods for <see cref="IWebThemeDefinition"/>.
/// </summary>
public static class WebThemeDefinitionExtensions
{
    extension(IWebThemeDefinition definition)
    {
        /// <summary>
        ///   Converts the theme definition to CSS.
        /// </summary>
        /// <returns>
        ///   The CSS content for the theme.
        /// </returns>
        public string ToCSS()
        {
            var css = new StringBuilder()
                .Append('\n')
                .Append($".theme-{definition.ID} {{\n");

            if (definition.Values.Count > 0)
                css.Append("  " + definition.Values.Select(pair => $" --{pair.Key}: {pair.Value};").Join("\n  ") + "\n");

            if (definition.Values.Count > 0 && !string.IsNullOrWhiteSpace(definition.CssContent))
                css.Append('\n');

            if (!string.IsNullOrWhiteSpace(definition.CssContent))
                css
                    .Append("  " + definition.CssContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : $"  {line.TrimEnd()}").Join("\n  ") + "\n");

            return css
                .AppendLine("}\n")
                .ToString();
        }
    }
}
