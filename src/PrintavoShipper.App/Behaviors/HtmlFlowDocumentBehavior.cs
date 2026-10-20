using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HtmlAgilityPack;
using DocumentList = System.Windows.Documents.List;

namespace PrintavoShipper.App.Behaviors;

/// <summary>
/// Renders Printavo's customerNote/productionNote HTML into a read-only
/// RichTextBox's FlowDocument — .NET (Core)/WPF dropped the old
/// System.Windows.Documents HTML-to-XAML converter, so this hand-rolls one
/// against the narrow, predictable tag set Printavo's rich-text editor
/// actually emits (confirmed live against real notes): div/br paragraph
/// breaks, strong/em bold/italic, mark for its highlight (drawn red here), and
/// ul/ol/li lists. Unrecognized tags fall back to rendering their text
/// content plainly rather than being dropped, so nothing silently vanishes.
/// </summary>
public static class HtmlFlowDocumentBehavior
{
    public static readonly DependencyProperty HtmlProperty = DependencyProperty.RegisterAttached(
        "Html", typeof(string), typeof(HtmlFlowDocumentBehavior), new PropertyMetadata(null, OnHtmlChanged));

    public static void SetHtml(RichTextBox element, string? value) => element.SetValue(HtmlProperty, value);

    public static string? GetHtml(RichTextBox element) => (string?)element.GetValue(HtmlProperty);

    private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBox richTextBox)
        {
            richTextBox.Document = Parse(e.NewValue as string);
        }
    }

    private static FlowDocument Parse(string? html)
    {
        var document = new FlowDocument { PagePadding = new Thickness(0) };

        if (string.IsNullOrWhiteSpace(html))
        {
            document.Blocks.Add(new Paragraph(new Run("(none)")));
            return document;
        }

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        Paragraph? loose = null;
        foreach (var node in htmlDoc.DocumentNode.ChildNodes)
        {
            AppendTopLevelNode(document, node, ref loose);
        }

        FlushLooseParagraph(document, ref loose);

        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(new Paragraph(new Run("(none)")));
        }

        return document;
    }

    /// <summary>
    /// Block-level tags (div/p/ul/ol) become their own Block; anything else
    /// at the top level (bare text, a stray br, an inline tag not wrapped in
    /// a div — all seen in real Printavo notes) accumulates into a single
    /// running paragraph that gets flushed once a real block tag is hit.
    /// </summary>
    private static void AppendTopLevelNode(FlowDocument document, HtmlNode node, ref Paragraph? loose)
    {
        switch (node.Name)
        {
            case "div":
            case "p":
                FlushLooseParagraph(document, ref loose);
                var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                AppendInlines(paragraph.Inlines, node.ChildNodes);
                document.Blocks.Add(paragraph);
                break;

            case "ul":
                FlushLooseParagraph(document, ref loose);
                document.Blocks.Add(BuildList(node, TextMarkerStyle.Disc));
                break;

            case "ol":
                FlushLooseParagraph(document, ref loose);
                document.Blocks.Add(BuildList(node, TextMarkerStyle.Decimal));
                break;

            default:
                loose ??= new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                AppendInlineNode(loose.Inlines, node);
                break;
        }
    }

    private static void FlushLooseParagraph(FlowDocument document, ref Paragraph? loose)
    {
        if (loose is { Inlines.Count: > 0 })
        {
            document.Blocks.Add(loose);
        }

        loose = null;
    }

    private static DocumentList BuildList(HtmlNode listNode, TextMarkerStyle style)
    {
        var list = new DocumentList { MarkerStyle = style, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var li in listNode.ChildNodes)
        {
            if (li.Name != "li")
            {
                continue;
            }

            var paragraph = new Paragraph();
            AppendInlines(paragraph.Inlines, li.ChildNodes);
            list.ListItems.Add(new ListItem(paragraph));
        }

        return list;
    }

    private static void AppendInlines(InlineCollection inlines, HtmlNodeCollection nodes)
    {
        foreach (var node in nodes)
        {
            AppendInlineNode(inlines, node);
        }
    }

    private static void AppendInlineNode(InlineCollection inlines, HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            if (!string.IsNullOrEmpty(node.InnerText))
            {
                inlines.Add(new Run(System.Net.WebUtility.HtmlDecode(node.InnerText)));
            }

            return;
        }

        if (node.Name == "br")
        {
            inlines.Add(new LineBreak());
            return;
        }

        // Everything else (strong/em/mark, or an unrecognized tag) becomes a
        // Span so its own children still render — formatting layers via
        // nested tags (e.g. <strong><mark>...) apply correctly since Spans
        // nest freely.
        var span = new Span();
        AppendInlines(span.Inlines, node.ChildNodes);

        switch (node.Name)
        {
            case "strong":
            case "b":
                span.FontWeight = FontWeights.Bold;
                break;
            case "em":
            case "i":
                span.FontStyle = FontStyles.Italic;
                break;
            case "mark":
                // Printavo's own highlight is yellow, which all but vanishes on
                // the Production Note's cream card — solid red with white text
                // stands out on any background and in either theme.
                span.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                span.Foreground = Brushes.White;
                break;
        }

        inlines.Add(span);
    }
}
