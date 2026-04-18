using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Users.UpdateUser;

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, UpdateUserResult>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public UpdateUserHandler(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UpdateUserResult> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"User with ID {command.Id} was not found.");

        user.Username = command.Username;
        user.Email = command.Email;
        user.Phone = command.Phone;
        user.Status = command.Status;
        user.Role = command.Role;
        user.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(user, cancellationToken);
        return _mapper.Map<UpdateUserResult>(updated);
    }
}
