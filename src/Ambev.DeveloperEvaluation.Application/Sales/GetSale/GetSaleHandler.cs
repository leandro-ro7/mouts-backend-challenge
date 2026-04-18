using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public class GetSaleHandler : IRequestHandler<GetSaleQuery, GetSaleResult>
{
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;

    public GetSaleHandler(ISaleRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<GetSaleResult> Handle(GetSaleQuery query, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {query.Id} was not found.");

        return _mapper.Map<GetSaleResult>(sale);
    }
}
