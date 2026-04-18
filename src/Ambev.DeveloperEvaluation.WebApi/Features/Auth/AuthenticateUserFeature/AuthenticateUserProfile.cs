using AutoMapper;
using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Auth.AuthenticateUserFeature;

public sealed class AuthenticateUserProfile : Profile
{
    public AuthenticateUserProfile()
    {
        CreateMap<AuthenticateUserRequest, AuthenticateUserCommand>();
        CreateMap<AuthenticateUserResult, AuthenticateUserResponse>();
    }
}
