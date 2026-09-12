using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace NoticeBoard.Rendering;

public enum InlineKind
{
    Word,
    Break,
    Icon,
    Itemstack,
}

public struct InlineElement
{
    public InlineKind Kind;
    public string Text;
    public bool Bold;
    public bool Italic;
    public bool Underline;
    public double[] Color;
    public bool IsBlock;
    public double FontSize;
    public string Family;
    public double LineHeight;
    public EnumTextOrientation Align;
}

// VTML subset for the physical paper: bold/italic, <a> links, <icon>, <itemstack>, <br>, and
// every vanilla <font> attribute from VtmlUtil.getFont (size, scale, family, color, opacity,
// weight, lineheight, align). Anything else degrades to its inner text only.
public static class PaperRichTextLayout
{
    public const double VtmlGuiSize = 18;

    private static readonly Regex TokenRegex = new(@"<[^>]+>|[^<]+", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex NewlineRegex = new(@"\r\n|\r|\n", RegexOptions.Compiled);
    private static readonly Regex BrRegex = new(@"^<br\s*/?>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BoldRegex = new(@"^</?(strong|b)>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ItalicRegex = new(@"^</?(i|em)>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LinkOpenRegex = new(@"^<a[\s>]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LinkCloseRegex = new(@"^</a>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SizeAttrRegex = new(@"size\s*=\s*[""']?(\d+(?:\.\d+)?)[""']?", RegexOptions.Compiled);
    private static readonly Regex ScaleAttrRegex = new(@"scale\s*=\s*[""']?(\d+(?:\.\d+)?)%[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FamilyAttrRegex = new(@"family\s*=\s*(?:[""']([^""']+)[""']|([^\s>]+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OpacityAttrRegex = new(@"opacity\s*=\s*[""']?(\d+(?:\.\d+)?)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LineHeightAttrRegex = new(@"lineheight\s*=\s*[""']?(-?\d+(?:\.\d+)?)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AlignAttrRegex = new(@"align\s*=\s*[""']?(left|right|center|justify)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WeightAttrRegex = new(@"weight\s*=\s*[""']?(bold|normal)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ColorAttrRegex = new(@"color\s*=\s*[""']?([^""'\s>]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NameAttrRegex = new(@"name\s*=\s*[""']?([A-Za-z0-9_\-]+)[""']?", RegexOptions.Compiled);
    private static readonly Regex TypeAttrRegex = new(@"type\s*=\s*[""']?(item|block)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CodeAttrRegex = new(@"code\s*=\s*[""']?([A-Za-z0-9:_\-\.\/]+)[""']?", RegexOptions.Compiled);

    private struct FontFrame
    {
        public bool ChangedColor;
        public double[] PrevColor;
        public bool ChangedWeight;
        public int PrevWeight;
        public bool ChangedSize;
        public double PrevSize;
        public bool ChangedFamily;
        public string PrevFamily;
        public bool ChangedLineHeight;
        public double PrevLineHeight;
        public bool ChangedAlign;
        public EnumTextOrientation PrevAlign;
    }

    public static List<InlineElement> Parse(string vtml, double[] defaultColor, double[] linkColor)
    {
        var result = new List<InlineElement>();
        if (string.IsNullOrEmpty(vtml))
            return result;

        int boldDepth = 0;
        int italicDepth = 0;
        int linkDepth = 0;
        int weightOverride = 0;
        double[] currentColor = defaultColor;
        double currentSize = 0;
        string currentFamily = null;
        double currentLineHeight = 0;
        EnumTextOrientation currentAlign = EnumTextOrientation.Left;
        var fontStack = new Stack<FontFrame>();
        bool prevWasBreak = false;

        bool IsBold() => weightOverride == 1 || (weightOverride != -1 && boldDepth > 0);

        InlineElement Styled(InlineKind kind, string text, bool isBlock = false) =>
            new InlineElement
            {
                Kind = kind,
                Text = text,
                Bold = IsBold(),
                Italic = italicDepth > 0,
                Underline = linkDepth > 0,
                Color = linkDepth > 0 ? linkColor : currentColor,
                IsBlock = isBlock,
                FontSize = currentSize,
                Family = currentFamily,
                LineHeight = currentLineHeight,
                Align = currentAlign,
            };

        // Two breaks in a row are a paragraph: reset bold/italic/link/font. A single <br> does not.
        void EmitBreak()
        {
            result.Add(new InlineElement { Kind = InlineKind.Break });

            if (prevWasBreak)
            {
                boldDepth = 0;
                italicDepth = 0;
                linkDepth = 0;
                weightOverride = 0;
                currentColor = defaultColor;
                currentSize = 0;
                currentFamily = null;
                currentLineHeight = 0;
                currentAlign = EnumTextOrientation.Left;
                fontStack.Clear();
            }

            prevWasBreak = true;
        }

        foreach (Match match in TokenRegex.Matches(vtml))
        {
            string token = match.Value;

            if (token[0] != '<')
            {
                string[] subLines = NewlineRegex.Split(token);
                for (int i = 0; i < subLines.Length; i++)
                {
                    if (i > 0)
                        EmitBreak();

                    foreach (string word in subLines[i].Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                    {
                        result.Add(Styled(InlineKind.Word, word));
                        prevWasBreak = false;
                    }
                }
                continue;
            }

            string tag = token.Trim();
            string lowerTag = tag.ToLowerInvariant();

            if (BrRegex.IsMatch(lowerTag))
            {
                EmitBreak();
                continue;
            }
            else if (BoldRegex.IsMatch(lowerTag))
            {
                boldDepth += lowerTag.StartsWith("</") ? -1 : 1;
                if (boldDepth < 0) boldDepth = 0;
            }
            else if (ItalicRegex.IsMatch(lowerTag))
            {
                italicDepth += lowerTag.StartsWith("</") ? -1 : 1;
                if (italicDepth < 0) italicDepth = 0;
            }
            else if (LinkOpenRegex.IsMatch(lowerTag))
            {
                linkDepth++;
            }
            else if (LinkCloseRegex.IsMatch(lowerTag))
            {
                linkDepth--;
                if (linkDepth < 0) linkDepth = 0;
            }
            else if (lowerTag.StartsWith("<font"))
            {
                var frame = new FontFrame
                {
                    PrevColor = currentColor,
                    PrevWeight = weightOverride,
                    PrevSize = currentSize,
                    PrevFamily = currentFamily,
                    PrevLineHeight = currentLineHeight,
                    PrevAlign = currentAlign,
                };

                Match colorMatch = ColorAttrRegex.Match(tag);
                if (colorMatch.Success)
                {
                    string raw = colorMatch.Groups[1].Value;
                    if (VtmlUtil.parseHexColor(raw, out double[] parsed) && parsed != null)
                    {
                        currentColor = parsed;
                        frame.ChangedColor = true;
                    }
                    else
                    {
                        double[] hex = ParseHexColor(raw, defaultColor[3]);
                        if (hex != null)
                        {
                            currentColor = hex;
                            frame.ChangedColor = true;
                        }
                    }
                }

                Match opacityMatch = OpacityAttrRegex.Match(tag);
                if (opacityMatch.Success
                    && double.TryParse(opacityMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double opacity))
                {
                    currentColor = (double[])currentColor.Clone();
                    if (currentColor.Length > 3)
                        currentColor[3] *= opacity;
                    frame.ChangedColor = true;
                }

                Match weightMatch = WeightAttrRegex.Match(tag);
                if (weightMatch.Success)
                {
                    weightOverride = weightMatch.Groups[1].Value.Equals("bold", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
                    frame.ChangedWeight = true;
                }

                double prevSize = currentSize > 0 ? currentSize : VtmlGuiSize;
                Match sizeMatch = SizeAttrRegex.Match(tag);
                if (sizeMatch.Success
                    && double.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSize)
                    && parsedSize > 0)
                {
                    currentSize = parsedSize;
                    frame.ChangedSize = true;
                }

                Match scaleMatch = ScaleAttrRegex.Match(tag);
                if (scaleMatch.Success
                    && double.TryParse(scaleMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double scaled)
                    && scaled > 0)
                {
                    currentSize = prevSize * scaled / 100.0;
                    frame.ChangedSize = true;
                }

                Match familyMatch = FamilyAttrRegex.Match(tag);
                if (familyMatch.Success)
                {
                    string family = familyMatch.Groups[1].Success ? familyMatch.Groups[1].Value : familyMatch.Groups[2].Value;
                    if (!string.IsNullOrEmpty(family))
                    {
                        currentFamily = family;
                        frame.ChangedFamily = true;
                    }
                }

                Match lineHeightMatch = LineHeightAttrRegex.Match(tag);
                if (lineHeightMatch.Success
                    && double.TryParse(lineHeightMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedLh))
                {
                    currentLineHeight = parsedLh;
                    frame.ChangedLineHeight = true;
                }

                Match alignMatch = AlignAttrRegex.Match(tag);
                if (alignMatch.Success)
                {
                    currentAlign = alignMatch.Groups[1].Value.ToLowerInvariant() switch
                    {
                        "right" => EnumTextOrientation.Right,
                        "center" => EnumTextOrientation.Center,
                        "justify" => EnumTextOrientation.Justify,
                        _ => EnumTextOrientation.Left,
                    };
                    frame.ChangedAlign = true;
                }

                fontStack.Push(frame);
            }
            else if (lowerTag == "</font>")
            {
                if (fontStack.Count > 0)
                {
                    FontFrame frame = fontStack.Pop();
                    if (frame.ChangedColor) currentColor = frame.PrevColor;
                    if (frame.ChangedWeight) weightOverride = frame.PrevWeight;
                    if (frame.ChangedSize) currentSize = frame.PrevSize;
                    if (frame.ChangedFamily) currentFamily = frame.PrevFamily;
                    if (frame.ChangedLineHeight) currentLineHeight = frame.PrevLineHeight;
                    if (frame.ChangedAlign) currentAlign = frame.PrevAlign;
                }
            }
            else if (lowerTag.StartsWith("<icon"))
            {
                Match nameMatch = NameAttrRegex.Match(tag);
                if (nameMatch.Success)
                    result.Add(Styled(InlineKind.Icon, nameMatch.Groups[1].Value));
            }
            else if (lowerTag.StartsWith("<itemstack"))
            {
                Match codeMatch = CodeAttrRegex.Match(tag);
                if (codeMatch.Success)
                {
                    Match typeMatch = TypeAttrRegex.Match(tag);
                    bool isBlock = typeMatch.Success && typeMatch.Groups[1].Value.Equals("block", StringComparison.OrdinalIgnoreCase);
                    result.Add(Styled(InlineKind.Itemstack, codeMatch.Groups[1].Value, isBlock));
                }
            }

            prevWasBreak = false;
        }

        return result;
    }

    // Icon/item size tracks vanilla VTML: bodyIconSize * (FontSize / VtmlGuiSize 18).
    public static double InlinePixelSize(InlineElement el, double bodyIconSize)
    {
        if (el.Kind != InlineKind.Icon && el.Kind != InlineKind.Itemstack)
            return bodyIconSize;
        return bodyIconSize * (el.FontSize > 0 ? el.FontSize / VtmlGuiSize : 1);
    }

    // Line height is the max of fonts on that line, not body alone. Wrap and Draw must use this.
    public static double LineHeight(ICoreClientAPI api, CairoFont baseFont, List<InlineElement> line, StyledFontCache cache)
    {
        double h = api.Gui.Text.GetLineHeight(baseFont);
        for (int i = 0; i < line.Count; i++)
        {
            InlineElement el = line[i];
            CairoFont f = el.Kind == InlineKind.Word || el.FontSize > 0 || el.LineHeight != 0
                ? cache.Get(el)
                : baseFont;
            double wordH = api.Gui.Text.GetLineHeight(f);
            if (wordH > h)
                h = wordH;
        }
        return h;
    }

    public static double LineWidth(Context ctx, CairoFont baseFont, List<InlineElement> line, double bodyIconSize, StyledFontCache cache)
    {
        PaperTextStyle.Setup(ctx, baseFont);
        double spaceWidth = ctx.TextExtents(" ").XAdvance;
        double width = 0;
        for (int i = 0; i < line.Count; i++)
        {
            InlineElement el = line[i];
            double w;
            if (el.Kind == InlineKind.Word)
            {
                PaperTextStyle.Setup(ctx, cache.Get(el));
                w = ctx.TextExtents(el.Text).XAdvance;
            }
            else
            {
                w = InlinePixelSize(el, bodyIconSize);
            }

            width += i > 0 ? w + spaceWidth : w;
        }
        return width;
    }

    // maxChars is expected to be a cheap performance backstop against pathologically long
    // message text (bounding per-word ctx.TextExtents work in Wrap on every mesh rebuild), set
    // far above any page's real capacity. The page's own overflow check is what actually
    // decides where displayed text cuts off, not this budget.
    public static (List<InlineElement> elements, bool wasCapped) CapTotalChars(List<InlineElement> elements, int maxChars)
    {
        var result = new List<InlineElement>(elements.Count);
        const int nonWordCost = 2;
        int count = 0;

        for (int i = 0; i < elements.Count; i++)
        {
            InlineElement el = elements[i];
            int cost = el.Kind switch
            {
                InlineKind.Word => el.Text?.Length ?? 0,
                InlineKind.Break => 0,
                _ => nonWordCost,
            };

            if (count > 0 && count + cost > maxChars)
                return (result, true);

            result.Add(el);
            count += cost;
        }

        return (result, false);
    }

    // ctx must already belong to the surface the elements will actually be drawn onto: text
    // measurement has to happen through the real Cairo context (not CairoFont's own internal
    // extents helper) so wrapping decisions exactly match what DrawTextLine later paints.
    public static List<List<InlineElement>> Wrap(Context ctx, CairoFont baseFont, List<InlineElement> elements, double maxWidth, double iconSize, StyledFontCache fontCache)
    {
        var lines = new List<List<InlineElement>>();
        var currentLine = new List<InlineElement>();
        double currentWidth = 0;

        PaperTextStyle.Setup(ctx, baseFont);
        double spaceWidth = ctx.TextExtents(" ").XAdvance;

        foreach (InlineElement el in elements)
        {
            if (el.Kind == InlineKind.Break)
            {
                lines.Add(currentLine);
                currentLine = new List<InlineElement>();
                currentWidth = 0;
                continue;
            }

            double width;
            if (el.Kind == InlineKind.Word)
            {
                CairoFont wordFont = fontCache.Get(el);
                PaperTextStyle.Setup(ctx, wordFont);
                width = ctx.TextExtents(el.Text).XAdvance;
            }
            else
            {
                width = InlinePixelSize(el, iconSize);
            }

            double neededWidth = currentLine.Count > 0 ? width + spaceWidth : width;

            if (currentLine.Count > 0 && currentWidth + neededWidth > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = new List<InlineElement>();
                currentWidth = 0;
                neededWidth = width;
            }

            currentLine.Add(el);
            currentWidth += neededWidth;
        }

        if (currentLine.Count > 0)
            lines.Add(currentLine);

        return lines;
    }

    public static CairoFont StyledFont(CairoFont baseFont, InlineElement word)
    {
        CairoFont font = baseFont.Clone();
        if (word.FontSize > 0)
            font = font.WithFontSize((float)(baseFont.UnscaledFontsize * (word.FontSize / VtmlGuiSize)));
        if (!string.IsNullOrEmpty(word.Family))
            font = font.WithFont(word.Family);
        if (word.Bold) font = font.WithWeight(FontWeight.Bold);
        if (word.Italic) font = font.WithSlant(FontSlant.Italic);
        double lh = word.LineHeight != 0 ? word.LineHeight : 1;
        font = font.WithLineHeightMultiplier(lh);
        return font.WithColor(word.Color);
    }

    private static double[] ParseHexColor(string hex, double alpha)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

        if (hex.Length < 6)
            return null;

        if (
            !byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)
            || !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)
            || !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b)
        )
        {
            return null;
        }

        return new double[] { r / 255.0, g / 255.0, b / 255.0, alpha };
    }
}

public sealed class StyledFontCache
{
    private readonly CairoFont baseFont;
    private readonly Dictionary<(bool Bold, bool Italic, string ColorKey, double FontSize, string Family, double LineHeight), CairoFont> cache = new();

    public StyledFontCache(CairoFont baseFont)
    {
        this.baseFont = baseFont;
    }

    public CairoFont Get(InlineElement word)
    {
        var key = (word.Bold, word.Italic, ColorKey(word.Color), word.FontSize, word.Family ?? "", word.LineHeight);
        if (!cache.TryGetValue(key, out CairoFont font))
        {
            font = PaperRichTextLayout.StyledFont(baseFont, word);
            cache[key] = font;
        }
        return font;
    }

    private static string ColorKey(double[] color)
    {
        return color == null ? "null" : string.Join(",", color);
    }
}
