using Ambev.DeveloperEvaluation.Application.Users.GetUser;

namespace Ambev.DeveloperEvaluation.Application.Users.ListUsers;

public class ListUsersResult
{
    public IEnumerable<GetUserResult> Data { get; set; } = Enumerable.Empty<GetUserResult>();
    public int TotalItems { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}
