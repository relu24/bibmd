using Bibui.Core.Runtime;
using Bibui.Text;

namespace BibMd;

/// <summary>Measuring and breaking text in one face, on bibui's text layer.</summary>
public static class Text
{
    /// <summary>The lines of <paramref name="text"/> at <paramref name="width"/>, broken between words.</summary>
    public static TextLine[] Lines(BibuiRuntime rt, string text, in Font font, float width) =>
        TextLayer.LinesFromText(rt.Fonts, text, TextLayer.StyleFromFont(rt.Fonts, font, font.Scale), font.Scale, width, TextWrap.Words).ToArray();

    /// <summary>Where the line the caret <paramref name="c"/> is on starts.</summary>
    public static int LineStart(string t, int c) => c == 0 ? 0 : t.LastIndexOf('\n', Math.Min(c, t.Length) - 1) + 1;

    /// <summary>How wide <paramref name="text"/> is in <paramref name="font"/>.</summary>
    public static float Width(BibuiRuntime rt, string text, in Font font) =>
        TextLayer.WidthFromChars(rt.Fonts, text, TextLayer.StyleFromFont(rt.Fonts, font, font.Scale), font.Scale);
}
