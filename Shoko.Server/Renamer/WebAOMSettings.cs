using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.Renamer;

public class WebAOMSettings : IRelocationProviderConfiguration
{
    [Display(
        Name = "Max Episode Length",
        Description = """
            The maximum length to truncate episode names to.
        """
    )]
    [Visibility(Size = DisplayElementSize.Large)]
    [Range(1, 250)]
    public int MaxEpisodeLength { get; set; } = 33;

    [Display(
        Name = "Use Group Aware Sorting",
        Description = """
            Whether to place files in a folder structure based on the Shoko group structure.
        """
    )]
    public bool GroupAwareSorting { get; set; } = false;

    [Display(
        Name = "Script",
        Description = """
            The WebAOM Script goes here.
        """
    )]
    [Visibility(Size = DisplayElementSize.Full)]
    [CodeEditor(CodeLanguage.PlainText)]
    public string Script { get; set; } = string.Empty;
}

public class WebAOMSettingsDefinition : IConfigurationDefinitionWithNewFactory<WebAOMSettings>
{
    public Type ConfigurationType => typeof(WebAOMSettings);

    public WebAOMSettings New() => new()
    {
        Script = """
// Sample Output: [Coalgirls]_Highschool_of_the_Dead_-_01_(1920x1080_Blu-ray_H264)_[90CC6DC1].mkv
// Sub group name
DO ADD '[%grp] '
// Anime Name, use english name if it exists, otherwise use the Romaji name
IF I(eng) DO ADD '%eng '
IF I(ann);I(!eng) DO ADD '%ann '
// Episode Number, don't use episode number for movies
IF T(!Movie) DO ADD '- %enr'
// If the file version is v2 or higher add it here
IF F(!1) DO ADD 'v%ver'
// Video Resolution
DO ADD ' (%res'
// Video Source (only if blu-ray or DVD)
IF R(DVD),R(Blu-ray) DO ADD ' %src'
// Video Codec
DO ADD ' %vid'
// Video Bit Depth (only if 10bit)
IF Z(10) DO ADD ' %bitbit'
DO ADD ') '
DO ADD '[%CRC]'

// Replacement rules (cleanup)
DO REPLACE ' ' '_' // replace spaces with underscores
DO REPLACE 'H264/AVC' 'H264'
DO REPLACE '0x0' ''
DO REPLACE '__' '_'
DO REPLACE '__' '_'

// Replace all illegal file name characters
DO REPLACE '<' '('
DO REPLACE '>' ')'
DO REPLACE ':' '-'
DO REPLACE '" + (char)34 +' '`'
DO REPLACE '/' '_'
DO REPLACE '/' '_'
DO REPLACE '\\' '_'
DO REPLACE '|' '_'
DO REPLACE '?' '_'
DO REPLACE '*' '_'
"""
    };
}
