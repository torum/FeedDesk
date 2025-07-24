using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using HtmlAgilityPack;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Image = Microsoft.UI.Xaml.Controls.Image;

// Code originaly adapted from https://blogs.msdn.microsoft.com/tess/2013/05/13/displaying-html-content-in-a-richtextblock/
// Modified from https://github.com/xleon/HTML2XAML/blob/master/XAMLHtml/XAMLHtml.cs

// Limitation: no relative uri support... needs base uri.

namespace FeedDesk.Helpers;

public class HtmlProperties : DependencyObject
{
    // TODO:
    //public record HtmlWithBaseUri(string html, Uri baseUri);

    public static readonly DependencyProperty HtmlProperty = DependencyProperty.RegisterAttached(
             "Html",
            typeof(string),
            typeof(HtmlProperties),
            new PropertyMetadata(string.Empty, HtmlChanged));

    public static void SetHtml(DependencyObject obj, string value) => obj.SetValue(HtmlProperty, value);

    public static string GetHtml(DependencyObject obj) => (string)obj.GetValue(HtmlProperty);
    
    /*
    public static Func<Span>? H1SpanFactory
    {
        get; set;
    }
    public static Func<Span>? H2SpanFactory
    {
        get; set;
    }
    public static Func<Span>? H3SpanFactory
    {
        get; set;
    }
    */

    public static Action<object, TappedRoutedEventArgs>? OnImageTapped
    {
        get; set;
    }

    // TODO:
    public static Uri? BaseUri
    {
        get; set;
    }

    public static Thickness ParagraphMargin { get; set; } = new Thickness(0, 12, 0, 12);
    public static Thickness BlockquoteMargin { get; set; } = new Thickness(24, 6, 0, 6);
    public static Thickness PreCodeMargin { get; set; } = new Thickness(24, 6, 0, 6);

    public static FontFamily PreCodeFontFamily { get; set; } = new FontFamily("Courier New");//Consolas //new FontFamily("monospace");

    public static int H1FontSize { get; set; } = 48;
    public static int H2FontSize { get; set; } = 32;
    public static int H3FontSize { get; set; } = 24;

    public static double ImageMaxPixelWidth { get; set; } = 650.0;
    public static double ImageMaxPixelHeight { get; set; } = 500.0;

    private static RichTextBlock? _currentObject;

    private static void HtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var richText = d as RichTextBlock;
        if (richText == null)
            return;

        richText.Blocks.Clear();

        _currentObject = richText;

        try
        {
            if (e.NewValue is string xhtml)
            {
                //Generate blocks
                var blocks = GenerateBlocksForHtml(xhtml);
                _currentObject = null;

                //Add the blocks to the RichTextBlock
                foreach (var b in blocks)
                {
                    richText.Blocks.Add(b);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("HtmlChanged: " + ex.ToString());
        }
    }

    private static List<Block> GenerateBlocksForHtml(string xhtml)
    {
        //Debug.WriteLine(xhtml);

        var blocks = new List<Block>();

        if (string.IsNullOrEmpty(xhtml))
        {
            return blocks;
        }

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(xhtml);

            if (doc.DocumentNode != null)
            {
                foreach (var node in doc.DocumentNode.ChildNodes)
                {
                    var block = GenerateBlockForTopNode(node);
                    if (block != null)
                    {
                        blocks.Add(block);
                    }
                }
                /*
                var block = GenerateParagraph(doc.DocumentNode);
                blocks.Add(block);
                */
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GenerateBlocksForHtml: " + ex.Message);
        }

        return blocks;
    }

    private static string CleanText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // No need to do this, since data is originaly coming from Feeds.Entry.Content.innerText.
        //return Windows.Data.Html.HtmlUtilities.ConvertToText(input);

        // Instead, convert HTML Entities such as "&nbsp;" to " ".
        return ReplaceHtmlEntities(input);
    }

    private static string ReplaceHtmlEntities(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return System.Net.WebUtility.HtmlDecode(input);
    }

    private static Block? GenerateBlockForTopNode(HtmlNode node)
    {
        if ((node.Name.ToLower() == "p") || (node.Name.ToLower() == "div") || (node.Name.ToLower() == "span"))
        {
            var paragraph = GenerateParagraph(node);
            return paragraph;
        }
        else if (node.Name.ToLower() == "blockquote")
        {
            var paragraph = GenerateParagraph(node);
            paragraph.Margin = BlockquoteMargin;
            paragraph.FontStyle = Windows.UI.Text.FontStyle.Italic;
            return paragraph;
        }
        else if (node.Name.ToLower() == "h1")
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            var span = new Span { FontSize = H1FontSize };
            var run = new Run { Text = CleanText(node.InnerText) };
            span.Inlines.Add(run);
            paragraph.Inlines.Add(span);
            return paragraph;
        }
        else if (node.Name.ToLower() == "h2")
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            var span = new Span { FontSize = H2FontSize };
            var run = new Run { Text = CleanText(node.InnerText) };
            span.Inlines.Add(run);
            paragraph.Inlines.Add(span);
            return paragraph;
        }
        else if (node.Name.ToLower() == "h3")
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            var span = new Span { FontSize = H3FontSize };
            var run = new Run { Text = CleanText(node.InnerText) };
            span.Inlines.Add(run);
            paragraph.Inlines.Add(span);
            return paragraph;
        }
        else if (node.Name.ToLower() == "h4")
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            var bold = new Bold();
            var run = new Run { Text = CleanText(node.InnerText) };
            bold.Inlines.Add(run);
            paragraph.Inlines.Add(bold);
            return paragraph;
        }
        else if (node.Name.ToLower() == "a")
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            //var link = GenerateHyperLink(node);
            var link = node.ChildNodes.Count >= 1 && (node.FirstChild.Name.ToLower() == "img")
                        ? GenerateImageWithHyperLink(node)
                        : GenerateHyperLink(node);
            if (link is not null)
            {
                paragraph.Inlines.Add(link);
                return paragraph;
            }
            else
            {
                return null;
            }
        }
        else if (node.Name.ToLower() == "img")
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            var img = GenerateImage(node);
            if (img is not null)
            {
                paragraph.Inlines.Add(img);
                return paragraph;
            }
            else
            {
                return null;
            }
        }
        else if ((node.Name.ToLower() == "pre") || (node.Name.ToLower() == "code"))
        {
            var paragraph = new Paragraph();
            //AddChildren(paragraph, node);
            paragraph.Inlines.Add(new Run { Text = CleanText(node.InnerText) });
            paragraph.Margin = PreCodeMargin;
            paragraph.FontFamily = PreCodeFontFamily;
            return paragraph;
        }
        else if ((node.Name.ToLower() == "b") || (node.Name.ToLower() == "strong"))
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            var bold = GenerateBold(node);
            if (bold is not null)
            {
                paragraph.Inlines.Add(bold);
                return paragraph;
            }
            else
            {
                return null;
            }
        }
        else if (node.Name.ToLower() == "iframe")
        {
            var paragraph = new Paragraph();
            paragraph.Margin = ParagraphMargin;
            var ifr = GenerateIFrame(node);
            if (ifr is not null)
            {
                paragraph.Inlines.Add(ifr);
                return paragraph;
            }
            else
            {
                return null;
            }
        }
        else if (node.Name.ToLower() == "#text")
        {
            if (!string.IsNullOrWhiteSpace(ReplaceHtmlEntities(node.InnerText)))
            {
                //Debug.WriteLine("#text: " + node.InnerText);
                var paragraph = new Paragraph();
                paragraph.Margin = ParagraphMargin;
                paragraph.Inlines.Add(new Run { Text = CleanText(node.InnerText) });
                return paragraph;
            }
            else
            {
                return null;
            }
        }
        else if ((node.Name.ToLower() == "script") || (node.Name.ToLower() == "style") || (node.Name.ToLower() == "link"))
        {
            return null;
        }
        else
        {
            //Debug.WriteLine("else: " + node.Name);
            var paragraph = GenerateParagraph(node);
            return paragraph;
        }
    }

    private static Block GenerateParagraph(HtmlNode node)
    {
        var paragraph = new Paragraph();
        paragraph.Margin = ParagraphMargin;
        AddChildren(paragraph, node);
        return paragraph;
    }

    private static void AddChildren(Paragraph p, HtmlNode node)
    {
        var added = false;

        foreach (var child in node.ChildNodes)
        {
            var i = GenerateInlineForNode(child);
            if (i != null)
            {
                p.Inlines.Add(i);
                added = true;
            }
        }
        if (!added)
        {
            if (!string.IsNullOrWhiteSpace(ReplaceHtmlEntities(node.InnerText)))
                p.Inlines.Add(new Run { Text = CleanText(node.InnerText) });
        }
    }

    private static void AddChildren(Span s, HtmlNode node)
    {
        var added = false;

        foreach (var child in node.ChildNodes)
        {
            var i = GenerateInlineForNode(child);
            if (i != null)
            {
                s.Inlines.Add(i);
                added = true;
            }
        }
        if (!added)
        {
            if (!string.IsNullOrWhiteSpace(ReplaceHtmlEntities(node.InnerText)))
                s.Inlines.Add(new Run { Text = CleanText(node.InnerText) });
        }
    }

    private static Inline? GenerateInlineForNode(HtmlNode node)
    {
        try
        {
            switch (node.Name.ToLower())
            {
                case "span":
                    return GenerateSpan(node);
                case "div":
                    return GenerateSpan(node);
                case "p":
                    return GenerateInnerParagraph(node);
                case "img":
                    return GenerateImage(node);
                case "a":
                    return node.ChildNodes.Count >= 1 && (node.FirstChild.Name.ToLower() == "img")
                        ? GenerateImageWithHyperLink(node)
                        : GenerateHyperLink(node);
                case "ul":
                    return GenerateUl(node);
                case "li":
                    return GenerateLi(node);
                case "b":
                case "strong":
                    return GenerateBold(node);
                case "i":
                case "em":
                    return GenerateItalic(node);
                case "u":
                    return GenerateUnderline(node);
                case "br":
                    return new LineBreak();
                case "script":
                    return null;
                case "style":
                    return null;
                case "link":
                    return null;
                // TODO:
                case "blockquote":
                    return GenerateInnerBlockquote(node);
                // TODO:
                case "pre":
                case "code":
                    return GenerateInnerPreCode(node);
                case "iframe":
                    return GenerateIFrame(node);
                case "#text":
                    if (!string.IsNullOrWhiteSpace(ReplaceHtmlEntities(node.InnerText)))
                        return new Run { Text = CleanText(node.InnerText) }; // CleanText is removing white spaces in this case
                                                                             //return new Run { Text = node.InnerText };
                    break;
                case "h1":
                    return GenerateH1(node);
                case "h2":
                    return GenerateH2(node);
                case "h3":
                    return GenerateH3(node);
                case "h4":
                    return GenerateH4(node);
                default:
                    //return null;
                    return GenerateSpanWNewLine(node);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("GenerateBlockForNode: " + e);
        }

        return null;
    }

    private static Inline GenerateImage(HtmlNode node)
    {
        var span = new Span();
        try
        {
            if (node.Attributes["src"] != null)
            {
                var inlineUiContainer = new InlineUIContainer();
                var sourceUri = WebUtility.HtmlDecode(node.Attributes["src"].Value);

                if (sourceUri.StartsWith("http"))
                {
                    var sourceWidth = WebUtility.HtmlDecode(node.Attributes["width"]?.Value);
                    var sourceHeight = WebUtility.HtmlDecode(node.Attributes["height"]?.Value);

                    var image = new Image
                    {
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    };

                    if (sourceWidth != null || sourceHeight != null)
                    {
                        //image.MaxWidth = ImageMaxPixelWidth;
                        if (sourceWidth != null)
                        {
                            //image.MaxWidth = double.Parse(sourceWidth);
                            //image.Width = double.Parse(sourceWidth);
                        }

                        //image.MaxHeight = ImageMaxPixelHeight;
                        if (sourceHeight != null)
                        {
                            //image.MaxHeight = double.Parse(sourceHeight);
                            //image.Height = double.Parse(sourceHeight);
                        }
                    }

                    image.ImageOpened += ImageOpened;
                    //image.MaxWidth = ImageMaxPixelWidth;
                    //image.MaxHeight = ImageMaxPixelHeight;

                    image.ImageFailed += ImageFailed;
                    image.Tapped += ImageOnTapped;

                    // hidden untill resize in ImageOpened.
                    image.Visibility = Visibility.Collapsed;

                    image.Source = new BitmapImage(new Uri(sourceUri, UriKind.Absolute));

                    inlineUiContainer.Child = image;

                    //span.Inlines.Add(new LineBreak());
                    span.Inlines.Add(inlineUiContainer);
                    span.Inlines.Add(new LineBreak());
                }
                else
                {
                    // TODO: 
                    span.Inlines.Add(new Run { Text = $" Image (relative URL): ({sourceUri}) " });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GenerateImage: " + ex.Message);
        }

        return span;
    }

    private static Inline? GenerateImageWithHyperLink(HtmlNode node)
    {
        // TODO:

        if (node.FirstChild is null)
        {
            return null;
        }

        node = node.FirstChild;

        var span = new Span();
        try
        {
            if (node.Attributes["src"] != null)
            {
                var inlineUiContainer = new InlineUIContainer();
                var sourceUri = WebUtility.HtmlDecode(node.Attributes["src"].Value);

                if (sourceUri.StartsWith("http"))
                {
                    var sourceWidth = WebUtility.HtmlDecode(node.Attributes["width"]?.Value);
                    var sourceHeight = WebUtility.HtmlDecode(node.Attributes["height"]?.Value);

                    var image = new Image
                    {
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    };

                    if (sourceWidth != null || sourceHeight != null)
                    {
                        //image.MaxWidth = ImageMaxPixelWidth;
                        if (sourceWidth != null)
                        {
                            //image.MaxWidth = double.Parse(sourceWidth);
                            //image.Width = double.Parse(sourceWidth);
                        }

                        //image.MaxHeight = ImageMaxPixelHeight;
                        if (sourceHeight != null)
                        {
                            //image.MaxHeight = double.Parse(sourceHeight);
                            //image.Height = double.Parse(sourceHeight);
                        }
                    }

                    image.ImageOpened += ImageOpened;
                    //image.MaxWidth = ImageMaxPixelWidth;
                    //image.MaxHeight = ImageMaxPixelHeight;

                    image.ImageFailed += ImageFailed;
                    image.Tapped += ImageOnTapped;

                    // hidden untill resize in ImageOpened.
                    image.Visibility = Visibility.Collapsed;

                    image.Source = new BitmapImage(new Uri(sourceUri, UriKind.Absolute));

                    inlineUiContainer.Child = image;

                    //span.Inlines.Add(new LineBreak());
                    span.Inlines.Add(inlineUiContainer);
                    span.Inlines.Add(new LineBreak());
                }
                else if (sourceUri.StartsWith("mailto:"))
                {
                    span.Inlines.Add(new Run { Text = $" Email Address: [mailto:]({sourceUri})" });
                }
                else
                {
                    // TODO: 
                    span.Inlines.Add(new Run { Text = $" Image (relative URL): ({sourceUri}) " });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GenerateImage: " + ex.Message);
        }
        return span;
    }

    private static void ImageOnTapped(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
    {
        // TODO:
        //OnImageTapped?.Invoke(sender, tappedRoutedEventArgs);
    }

    private static void ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        Debug.WriteLine("image failed to load: " + e.ErrorMessage);
    }

    private static void ImageOpened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Image img) return;

            var bimg = img.Source as BitmapImage;
            if (bimg == null) return;

            img.MaxHeight = bimg.PixelHeight;
            img.MaxWidth = bimg.PixelWidth;

            /*
            if (bimg.PixelWidth > ImageMaxPixelWidth || bimg.PixelHeight > ImageMaxPixelHeight)
            {
                //img.MaxWidth = ImageMaxPixelWidth;
                //img.MaxHeight = ImageMaxPixelHeight;
                img.Width = ImageMaxPixelWidth;
                img.Height = ImageMaxPixelHeight;

                if (bimg.PixelWidth > ImageMaxPixelWidth)
                {
                    img.Width = ImageMaxPixelWidth;
                    img.Height = (ImageMaxPixelWidth / bimg.PixelWidth) * bimg.PixelHeight;
                }
                if (img.Height > ImageMaxPixelHeight)
                {
                    img.Height = ImageMaxPixelHeight;
                    img.Width = (ImageMaxPixelHeight / img.Height) * img.Width;
                }
            }
            else
            {
                img.Height = bimg.PixelHeight;
                img.Width = bimg.PixelWidth;
            }
            */

            img.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("ImageOpened: " + ex.Message);
        }
    }

    private static Inline? GenerateHyperLink(HtmlNode node)
    {
        if (node.Attributes == null)
        {
            Debug.WriteLine("GenerateHyperLink node.Attributes is null.");
            return null;
        }
        if (node.Attributes["href"] == null)
        {
            Debug.WriteLine("GenerateHyperLink node.Attributes[\"href\"] is null.");
            return null;
        }

        try
        {
            if (node.Attributes["href"] != null)
            {
                var href = node.Attributes["href"].Value;
                if (!string.IsNullOrEmpty(href))
                {
                    if (href.StartsWith("http"))
                    {
                        var hyperlinkButton = new Hyperlink();
                        hyperlinkButton.NavigateUri = new Uri(href, UriKind.Absolute);
                        hyperlinkButton.Inlines.Add(new Run { Text = CleanText(node.InnerText) });
                        var span = new Span();
                        span.Inlines.Add(hyperlinkButton);
                        return span;
                    }
                    else if (href.StartsWith("mailto:"))
                    {
                        var span = new Span();
                        span.Inlines.Add(new Run { Text = $" Email Address: [mailto:]({CleanText(node.InnerText)}) " });
                        return span;
                    }
                    else
                    {
                        // TODO: handle baseUrl
                        Debug.WriteLine("GenerateHyperLink Relative Uri!!");

                        var span = new Span();
                        span.Inlines.Add(new Run { Text = $" Url (relative): [{CleanText(node.InnerText)}]({href}) " });
                        return span;
                    }
                }
            }

            return null;
            /*
                        var span = new Span();
                        var inlineUiContainer = new InlineUIContainer();
                        var hyperlinkButton = new Hyperlink//new HyperlinkButton
                        {
                            NavigateUri = new Uri(node.Attributes["href"].Value, UriKind.Absolute),
                            //Content = CleanText(node.InnerText)
                        };
                        */
            /*
            if (node.ParentNode != null && (node.ParentNode.Name == "li" || node.ParentNode.Name == "LI"))
                hyperlinkButton.Style = (Style)Application.Current.Resources["RTLinkLI"];
            else if ((node.NextSibling == null
                || string.IsNullOrWhiteSpace(node.NextSibling.InnerText)) && (node.PreviousSibling == null
                || string.IsNullOrWhiteSpace(node.PreviousSibling.InnerText)))
                hyperlinkButton.Style = (Style)Application.Current.Resources["RTLinkOnly"];
            else
                hyperlinkButton.Style = (Style)Application.Current.Resources["RTLink"];
            */
            /*
            inlineUiContainer.Child = hyperlinkButton;
            span.Inlines.Add(inlineUiContainer);
            return span;
            */
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GenerateHyperLink: " + ex.Message);
            return null;
        }
    }

    private static Inline? GenerateIFrame(HtmlNode node)
    {
        try
        {
            if (node.Attributes != null)
            {
                var src = string.Empty;
                if (node.Attributes["src"] != null)
                    src = node.Attributes["src"].Value;
                var title = string.Empty;
                if (node.Attributes["title"] != null)
                    title = node.Attributes["title"].Value;

                if (!string.IsNullOrEmpty(src))
                {
                    var span = new Span();
                    span.Inlines.Add(new LineBreak());

                    if (src.StartsWith("http"))
                    {
                        var hyperlinkButton = new Hyperlink();
                        hyperlinkButton.NavigateUri = new Uri(src, UriKind.Absolute);
                        if (!string.IsNullOrEmpty(title))
                        {
                            hyperlinkButton.Inlines.Add(new Run { Text = $"Inline frame (embedded page): [{CleanText(title)}]" });
                        }
                        else
                        {
                            //hyperlinkButton.Inlines.Add(new Run { Text = $"iframe: ({src})" });
                            hyperlinkButton.Inlines.Add(new Run { Text = $"Inline frame (embedded page): (no title)" });
                        }
                        span.Inlines.Add(hyperlinkButton);
                    }
                    else
                    {
                        // md like..
                        //span.Inlines.Add(new Run { Text = CleanText($"[Inline frame (embedded page) (relative url): {CleanText(title)}]({src})") });
                        span.Inlines.Add(new Run { Text = CleanText($"Inline frame (embedded page) (relative URL): [{CleanText(title)}]({src})") });
                    }

                    span.Inlines.Add(new LineBreak());
                    return span;
                }

            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GenerateIFrame: " + ex.Message);
            return null;
        }
    }

    private static Inline GenerateBold(HtmlNode node)
    {
        var bold = new Bold();
        AddChildren(bold, node);
        return bold;
    }

    private static Inline GenerateUnderline(HtmlNode node)
    {
        var underline = new Underline();
        AddChildren(underline, node);
        return underline;
    }

    private static Inline GenerateItalic(HtmlNode node)
    {
        var italic = new Italic();
        AddChildren(italic, node);
        return italic;
    }

    private static Inline GenerateUl(HtmlNode node)
    {
        var span = new Span();
        span.Inlines.Add(new LineBreak());
        AddChildren(span, node);
        return span;
    }

    private static Inline GenerateLi(HtmlNode node)
    {
        var span = new Span();
        var inlineUiContainer = new InlineUIContainer();

        var ellipse = new Ellipse
        {
            Fill = _currentObject?.Foreground ?? new SolidColorBrush(Colors.Gainsboro),
            Width = 6,
            Height = 6,
            Margin = new Thickness(-24, 0, 0, 1)
        };
        inlineUiContainer.Child = ellipse;

        // Indent(tab)
        span.Inlines.Add(new Run { Text = "\t" });

        span.Inlines.Add(inlineUiContainer);
        AddChildren(span, node);
        span.Inlines.Add(new LineBreak());
        return span;
    }

    private static Inline GenerateInnerParagraph(HtmlNode node)
    {
        var span = new Span();
        span.Inlines.Add(new LineBreak());
        AddChildren(span, node);
        span.Inlines.Add(new LineBreak());
        return span;
    }

    private static Inline GenerateInnerBlockquote(HtmlNode node)
    {
        var italic = new Italic();
        italic.Inlines.Add(new LineBreak());
        italic.Inlines.Add(new LineBreak());
        AddChildren(italic, node);
        italic.Inlines.Add(new LineBreak());
        italic.Inlines.Add(new LineBreak());
        return italic;
    }

    private static Inline GenerateSpan(HtmlNode node)
    {
        var span = new Span();
        AddChildren(span, node);
        return span;
    }

    private static Inline GenerateInnerPreCode(HtmlNode node)
    {
        // TODO: all line breaks will be gone.
        return new Run
        {
            Text = CleanText(node.InnerText)
        };
        /*
        var span = new Span();
        span.FontFamily = PreCodeFontFamily;
        AddChildren(span, node);
        return span;
        */
    }

    private static Inline GenerateSpanWNewLine(HtmlNode node)
    {
        var span = new Span();
        AddChildren(span, node);
        if (span.Inlines.Count > 0)
            span.Inlines.Add(new LineBreak());
        return span;
    }

    private static Inline GenerateH4(HtmlNode node)
    {
        //var span = H3SpanFactory?.Invoke() ?? new Span();
        var span = new Span();
        span.Inlines.Add(new LineBreak());
        var bold = new Bold();
        var run = new Run { Text = CleanText(node.InnerText) };
        bold.Inlines.Add(run);
        span.Inlines.Add(bold);
        span.Inlines.Add(new LineBreak());
        return span;
    }

    private static Inline GenerateH3(HtmlNode node)
    {
        //var span = H3SpanFactory?.Invoke() ?? new Span { FontSize = H3FontSize };
        var span = new Span { FontSize = H3FontSize };
        span.Inlines.Add(new LineBreak());
        var bold = new Bold();
        var run = new Run { Text = CleanText(node.InnerText) };
        bold.Inlines.Add(run);
        span.Inlines.Add(bold);
        span.Inlines.Add(new LineBreak());
        return span;
    }

    private static Inline GenerateH2(HtmlNode node)
    {
        //var span = H2SpanFactory?.Invoke() ?? new Span { FontSize = H2FontSize };
        var span = new Span { FontSize = H2FontSize };
        span.Inlines.Add(new LineBreak());
        var run = new Run { Text = CleanText(node.InnerText) };
        span.Inlines.Add(run);
        span.Inlines.Add(new LineBreak());
        return span;
    }

    private static Inline GenerateH1(HtmlNode node)
    {
        //var span = H1SpanFactory?.Invoke() ?? new Span { FontSize = H1FontSize };
        var span = new Span { FontSize = H1FontSize };
        span.Inlines.Add(new LineBreak());
        var run = new Run { Text = CleanText(node.InnerText) };
        span.Inlines.Add(run);
        span.Inlines.Add(new LineBreak());
        return span;
    }
}
