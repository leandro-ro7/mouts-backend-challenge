using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Entities;
using AutoMapper;

namespace Ambev.DeveloperEvaluation.Application.Sales;

public class SalesCommonProfile : Profile
{
    public SalesCommonProfile()
    {
        CreateMap<SaleItem, SaleItemResult>()
            .ForMember(d => d.Discount, o => o.MapFrom(s => s.Discount.Value));
    }
}
