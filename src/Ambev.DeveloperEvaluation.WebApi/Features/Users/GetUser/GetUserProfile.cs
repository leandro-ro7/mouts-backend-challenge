using AutoMapper;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Users.GetUser;

public class GetUserProfile : Profile
{
    public GetUserProfile()
    {
        CreateMap<Guid, GetUserCommand>()
            .ConstructUsing(id => new GetUserCommand(id));

        CreateMap<GetUserResult, GetUserResponse>();
    }
}
