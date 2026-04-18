using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Users.ListUsers;

public class ListUsersHandler : IRequestHandler<ListUsersQuery, ListUsersResult>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public ListUsersHandler(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ListUsersResult> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.ListAsync(
            query.Page, query.Size, query.Order, cancellationToken);

        return new ListUsersResult
        {
            Data = _mapper.Map<IEnumerable<GetUserResult>>(items),
            TotalItems = totalCount,
            CurrentPage = query.Page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.Size)
        };
    }
}
