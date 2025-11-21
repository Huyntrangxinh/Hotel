using HotelBooking.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using Microsoft.Extensions.Localization;
using HotelBooking.Resources;

namespace HotelBooking.Documents
{
    public class ContractDocument : IDocument
    {
        private readonly ReviewViewModel _model;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private const string CommissionRate = "15%";

        public ContractDocument(ReviewViewModel model, IStringLocalizer<SharedResource> localizer)
        {
            _model = model;
            _localizer = localizer;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(50);
                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span(_localizer["Page"] + " ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
        }

        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(_localizer["HotelCooperationAgreement"])
                        .Bold().FontSize(18).AlignCenter();
                    column.Item().Text("HOTEL COOPERATION AGREEMENT")
                        .SemiBold().FontSize(14).AlignCenter();
                });
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(25);

                // SỬA LẠI: Áp dụng style sau khi gọi .Text()
                column.Item().Text(string.Format(_localizer["ContractDate"], DateTime.Now.Day, DateTime.Now.Month, DateTime.Now.Year)).FontSize(10).Italic();

                column.Item().Element(c => ComposeSection(c, _localizer["PartyA"], table =>
                {
                    table.Cell().Text(_localizer["LegalEntityName"]).SemiBold();
                    table.Cell().Text(_model.LegalEntityName ?? _localizer["NotProvided"]);
                    table.Cell().Text(_localizer["RegisteredAddress"]).SemiBold();
                    table.Cell().Text(_model.LegalEntityAddress ?? _localizer["NotProvided"]);
                    table.Cell().Text(_localizer["RepresentedBy"]).SemiBold();
                    table.Cell().Text($"{_model.SignatoryName} - {_localizer["Position"]} {_model.SignatoryPosition}");
                    table.Cell().Text(_localizer["EmailAddress"]).SemiBold();
                    table.Cell().Text(_model.SignatoryEmail ?? _localizer["NotProvided"]);
                }));

                column.Item().Element(c => ComposeSection(c, _localizer["PartyB"], table =>
                {
                    table.Cell().Text(_localizer["CompanyName"]).SemiBold();
                    table.Cell().Text("Booking.com B.V.");
                    table.Cell().Text(_localizer["Address"]).SemiBold();
                    table.Cell().Text(_localizer["BookingCompanyAddress"]);
                }));

                column.Item().Text(_localizer["PartiesAgreement"]).FontSize(10);
                
                // SỬA LẠI: Chuyển .PaddingTop() ra trước .Text()
                column.Item().PaddingTop(10).Text(_localizer["TermsAndConditions"]).Bold().FontSize(14).Underline();

                column.Item().Element(c => ComposeParagraph(c, _localizer["Article1"], _localizer["Article1Content"]));
                column.Item().Element(c => ComposeParagraph(c, _localizer["Article2"], _localizer["Article2Content"]));
                column.Item().Element(c => ComposeParagraph(c, _localizer["Article3"], string.Format(_localizer["Article3Content"], CommissionRate)));
                column.Item().Element(c => ComposeParagraph(c, _localizer["Article4"], _localizer["Article4Content"]));
                column.Item().Element(c => ComposeParagraph(c, _localizer["Article5"], _localizer["Article5Content"]));

                column.Item().PaddingTop(50).Row(row =>
                {
                    row.RelativeItem().Column(col => {
                        col.Item().AlignCenter().Text(_localizer["PartyARepresentative"]).Bold();
                        col.Item().AlignCenter().Text(_localizer["SignAndStamp"]);
                        col.Item().Height(80);
                        col.Item().AlignCenter().Text(_model.SignatoryName ?? "..............................").Underline();
                        col.Item().AlignCenter().Text($"{_localizer["Position"]} {_model.SignatoryPosition}");
                    });
                    row.RelativeItem().Column(col => {
                        col.Item().AlignCenter().Text(_localizer["PartyBRepresentative"]).Bold();
                        col.Item().AlignCenter().Text(_localizer["SignAndStamp"]);
                        col.Item().Height(80);
                        col.Item().AlignCenter().Text("..............................").Underline();
                        col.Item().AlignCenter().Text(_localizer["ChiefExecutiveOfficer"]);
                    });
                });
            });
        }
        
        void ComposeSection(IContainer container, string title, Action<TableDescriptor> content)
        {
            container.ShowEntire().Column(column =>
            {
                // SỬA LẠI: Chuyển .PaddingBottom() ra trước .Text()
                column.Item().PaddingBottom(5).Text(title).SemiBold().FontSize(11);
                column.Item().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(100);
                            columns.RelativeColumn();
                        });
                        content(table);
                    });
                });
            });
        }
        
        void ComposeParagraph(IContainer container, string title, string text)
        {
            container.ShowEntire().Column(column =>
            {
                column.Spacing(5);
                column.Item().Text(title).SemiBold().FontSize(11);
                column.Item().Text(text).FontSize(10).LineHeight(1.5f);
            });
        }
    }
}