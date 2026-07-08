using Accountrack.SharedKernel.Pdf;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using Microsoft.AspNetCore.Http;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Accountrack.Web.Common.Pdf;

/// <summary>
/// Renders a <see cref="PdfDocument"/> to a styled, modern business-document PDF via QuestPDF
/// (ADR-0031). Clean layout: brand-accent title + table header, generous whitespace, right-aligned
/// tabular money, an emphasised grand total. Currency-/number-formatting is done by the caller.
/// </summary>
public static class PdfRenderer
{
    private const string Ink = "#111827";
    private const string Muted = "#6B7280";
    private const string Faint = "#9CA3AF";
    private const string Hair = "#E5E7EB";
    private const string Zebra = "#F9FAFB";

    /// <summary>The brand typeface (SIL OFL 1.1), embedded and registered below so PDFs match the SPA.</summary>
    private const string Brand = "Plus Jakarta Sans";

    // Register the embedded Plus Jakarta Sans weights with QuestPDF once, so documents and reports
    // render in the brand typeface regardless of the fonts installed on the host (e.g. the container).
    static PdfRenderer()
    {
        var assembly = typeof(PdfRenderer).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is not null)
            {
                FontManager.RegisterFont(stream);
            }
        }
    }

    /// <summary>Brand logo mark (teal rounded square + ascending bars) — kept in sync with docs/frontend/brand.</summary>
    private const string LogoSvg = """
        <svg width="40" height="40" viewBox="0 0 40 40" xmlns="http://www.w3.org/2000/svg">
          <rect width="40" height="40" rx="11" fill="#007E6E"/>
          <g fill="#FFFFFF">
            <rect x="11" y="21" width="5" height="8" rx="2"/>
            <rect x="17.5" y="16" width="5" height="13" rx="2"/>
            <rect x="24" y="11" width="5" height="18" rx="2"/>
          </g>
        </svg>
        """;

    public static byte[] Render(PdfDocument model)
    {
        var accent = model.AccentHex;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(38);
                page.DefaultTextStyle(t => t.FontFamily(Brand).FontSize(10).FontColor(Ink).LineHeight(1.25f));

                page.Header().Element(e => Header(e, model, accent));
                page.Content().PaddingTop(18).Element(e => Body(e, model, accent));
                page.Footer().Element(e => Footer(e, model.FooterNote));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, PdfDocument m, string accent)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Width(34).Height(34).Svg(LogoSvg);
                col.Item().PaddingTop(8).Text(m.Seller.Name).FontSize(16).Bold().FontColor(accent);
                foreach (var line in m.Seller.Lines)
                {
                    col.Item().Text(line).FontSize(9).FontColor(Muted);
                }
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(m.Title.ToUpperInvariant()).FontSize(24).Bold().FontColor(accent);
                col.Item().AlignRight().Text(m.Number).FontSize(11).FontColor(Muted);
            });
        });
    }

    private static void Body(IContainer container, PdfDocument m, string accent)
    {
        container.Column(col =>
        {
            col.Spacing(16);

            // Bill-to + meta
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(m.BillToLabel.ToUpperInvariant()).FontSize(8).Bold().FontColor(Faint).LetterSpacing(0.05f);
                    c.Item().PaddingTop(3).Text(m.BillTo.Name).Bold();
                    foreach (var line in m.BillTo.Lines)
                    {
                        c.Item().Text(line).FontSize(9).FontColor(Muted);
                    }
                });

                row.ConstantItem(220).Column(c =>
                {
                    foreach (var field in m.Meta)
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text(field.Label).FontSize(9).FontColor(Muted);
                            r.RelativeItem().AlignRight().Text(field.Value).FontSize(9).SemiBold();
                        });
                    }
                });
            });

            // Line items
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(2.2f);
                });

                table.Header(header =>
                {
                    void Head(string text, bool right = false)
                    {
                        var cell = header.Cell().Background(accent).PaddingVertical(6).PaddingHorizontal(8);
                        (right ? cell.AlignRight() : cell).Text(text).FontColor("#FFFFFF").FontSize(9).SemiBold();
                    }

                    Head("Description");
                    Head("Qty", right: true);
                    Head("Unit price", right: true);
                    Head("Tax", right: true);
                    Head("Amount", right: true);
                });

                var i = 0;
                foreach (var line in m.Lines)
                {
                    var bg = i++ % 2 == 1 ? Zebra : "#FFFFFF";
                    IContainer Cell(bool right = false)
                    {
                        var cell = table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Hair).PaddingVertical(5).PaddingHorizontal(8);
                        return right ? cell.AlignRight() : cell;
                    }

                    Cell().Text(line.Description);
                    Cell(true).Text(line.Quantity);
                    Cell(true).Text(line.UnitPrice);
                    Cell(true).Text(line.Tax).FontColor(Muted);
                    Cell(true).Text(line.Amount);
                }
            });

            // Totals
            col.Item().AlignRight().Width(260).Column(c =>
            {
                foreach (var total in m.Totals)
                {
                    if (total.Emphasis)
                    {
                        c.Item().PaddingTop(4).BorderTop(1).BorderColor(accent).PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text(total.Label).Bold().FontColor(accent);
                            r.RelativeItem().AlignRight().Text(total.Value).FontSize(13).Bold().FontColor(accent);
                        });
                    }
                    else
                    {
                        c.Item().PaddingVertical(2).Row(r =>
                        {
                            r.RelativeItem().Text(total.Label).FontColor(Muted);
                            r.RelativeItem().AlignRight().Text(total.Value).SemiBold();
                        });
                    }
                }
            });

            if (!string.IsNullOrWhiteSpace(m.Notes))
            {
                col.Item().PaddingTop(6).Column(c =>
                {
                    c.Item().Text("NOTES").FontSize(8).Bold().FontColor(Faint).LetterSpacing(0.05f);
                    c.Item().PaddingTop(3).Text(m.Notes!).FontSize(9).FontColor(Muted);
                });
            }
        });
    }

    private static void Footer(IContainer container, string? footerNote)
    {
        container.BorderTop(0.5f).BorderColor(Hair).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(footerNote ?? string.Empty).FontSize(8).FontColor(Faint);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Faint));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    /// <summary>Turns a successful document model into a downloadable PDF response.</summary>
    public static async Task<IResult> File(Task<Result<PdfDocument>> task, string fileName)
    {
        var result = await task;
        return result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.File(Render(result.Value), "application/pdf", $"{fileName}.pdf")
            : result.ToHttpResult();
    }

    // ---- Financial reports ----

    public static byte[] RenderReport(PdfReport model)
    {
        var accent = model.AccentHex;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(38);
                page.DefaultTextStyle(t => t.FontFamily(Brand).FontSize(10).FontColor(Ink).LineHeight(1.2f));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(30).Height(30).Svg(LogoSvg);
                        row.RelativeItem().PaddingLeft(10).AlignMiddle()
                            .Text(model.Company.Name).FontSize(12).Bold().FontColor(accent);
                    });
                    col.Item().PaddingTop(8).Text(model.Title).FontSize(18).Bold();
                    if (!string.IsNullOrWhiteSpace(model.Period))
                    {
                        col.Item().Text(model.Period!).FontSize(9).FontColor(Muted);
                    }
                });

                page.Content().PaddingTop(14).Element(e => ReportTable(e, model, accent));
                page.Footer().Element(e => Footer(e, model.FooterNote));
            });
        }).GeneratePdf();
    }

    private static void ReportTable(IContainer container, PdfReport m, string accent)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4);
                for (var i = 1; i < m.Columns.Count; i++)
                {
                    c.RelativeColumn(2);
                }
            });

            table.Header(header =>
            {
                for (var i = 0; i < m.Columns.Count; i++)
                {
                    var cell = header.Cell().Background(accent).PaddingVertical(6).PaddingHorizontal(8);
                    (i == 0 ? cell : cell.AlignRight()).Text(m.Columns[i]).FontColor("#FFFFFF").FontSize(9).SemiBold();
                }
            });

            foreach (var row in m.Rows)
            {
                for (var i = 0; i < m.Columns.Count; i++)
                {
                    var value = i < row.Cells.Count ? row.Cells[i] : null;
                    IContainer cell = table.Cell();

                    cell = row.Style switch
                    {
                        PdfRowStyle.SectionHeader => cell.PaddingTop(10).PaddingBottom(2).PaddingHorizontal(8),
                        PdfRowStyle.GrandTotal => cell.BorderTop(1).BorderColor(accent).PaddingVertical(6).PaddingHorizontal(8),
                        PdfRowStyle.Subtotal => cell.BorderTop(0.5f).BorderColor(Hair).PaddingVertical(5).PaddingHorizontal(8),
                        _ => cell.PaddingVertical(4).PaddingHorizontal(8),
                    };

                    var aligned = i == 0 ? cell : cell.AlignRight();
                    var text = aligned.Text(value ?? string.Empty);

                    switch (row.Style)
                    {
                        case PdfRowStyle.SectionHeader: text.SemiBold().FontColor(Muted).FontSize(9); break;
                        case PdfRowStyle.GrandTotal: text.Bold().FontColor(accent).FontSize(11); break;
                        case PdfRowStyle.Subtotal: text.SemiBold(); break;
                        default: if (i > 0) text.FontColor(Ink); break;
                    }
                }
            }
        });
    }

    /// <summary>Turns a successful report model into a downloadable PDF response.</summary>
    public static async Task<IResult> ReportFile(Task<Result<PdfReport>> task, string fileName)
    {
        var result = await task;
        return result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.File(RenderReport(result.Value), "application/pdf", $"{fileName}.pdf")
            : result.ToHttpResult();
    }
}
