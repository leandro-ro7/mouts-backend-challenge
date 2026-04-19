using Ambev.DeveloperEvaluation.Domain.Entities;
using AutoMapper;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesSummaryProfile : Profile
{
    public ListSalesSummaryProfile()
    {
        CreateMap<Sale, SaleSummaryResult>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count(i => !i.IsCancelled)));
    }
}
