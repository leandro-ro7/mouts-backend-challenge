using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesHandler : IRequestHandler<ListSalesQuery, ListSalesResult>
{
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;

    public ListSalesHandler(ISaleRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ListSalesResult> Handle(ListSalesQuery query, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.ListAsync(
            query.Page, query.Size, query.Order, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.Size);

        return new ListSalesResult
        {
            Data = _mapper.Map<IEnumerable<CreateSaleResult>>(items),
            TotalItems = totalCount,
            CurrentPage = query.Page,
            TotalPages = totalPages
        };
    }
}
