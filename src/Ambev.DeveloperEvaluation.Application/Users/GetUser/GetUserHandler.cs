using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Users.GetUser;

public class GetUserHandler : IRequestHandler<GetUserCommand, GetUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public GetUserHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<GetUserResult> Handle(GetUserCommand request, CancellationToken cancellationToken)
    {
        // Validation runs via ValidationBehavior pipeline before this handler is called
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {request.Id} not found");

        return _mapper.Map<GetUserResult>(user);
    }
}
