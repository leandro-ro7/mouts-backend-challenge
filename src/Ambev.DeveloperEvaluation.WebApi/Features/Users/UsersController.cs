using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Application.Users.DeleteUser;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Application.Users.ListUsers;
using Ambev.DeveloperEvaluation.Application.Users.UpdateUser;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.CreateUser;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.GetUser;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Users;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : BaseController
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;

    public UsersController(IMediator mediator, IMapper mapper)
    {
        _mediator = mediator;
        _mapper = mapper;
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseWithData<CreateUserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<CreateUserCommand>(request);
        var response = await _mediator.Send(command, cancellationToken);

        return Created(string.Empty, new ApiResponseWithData<CreateUserResponse>
        {
            Success = true,
            Message = "User created successfully",
            Data = _mapper.Map<CreateUserResponse>(response)
        });
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseWithData<ListUsersResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int _page = 1,
        [FromQuery] int _size = 10,
        [FromQuery] string? _order = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListUsersQuery { Page = _page, Size = _size, Order = _order }, cancellationToken);
        return Ok(new ApiResponseWithData<ListUsersResult> { Success = true, Data = result });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<GetUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<GetUserCommand>(id);
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponseWithData<GetUserResponse>
        {
            Success = true,
            Message = "User retrieved successfully",
            Data = _mapper.Map<GetUserResponse>(response)
        });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseWithData<UpdateUserResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<UpdateUserCommand>(request);
        command.Id = id;
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponseWithData<UpdateUserResult> { Success = true, Data = result });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var command = _mapper.Map<DeleteUserCommand>(id);
        await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse { Success = true, Message = "User deleted successfully" });
    }
}
