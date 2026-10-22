using FSIdentity.Domain.Enums;
using FSIdentity.Domain.Exceptions;

namespace FSIdentity.Tests.Common;

public class ExceptionMapperTests
{
    public static TheoryData<Exception, int, string> Cases => new()
    {
        { new NotFoundException("no patient"), 404, "NOT_FOUND" },
        { new ConflictException("stock changed"), 409, "CONFLICT" },
        { new ForbiddenException("not yours"), 403, "FORBIDDEN" },
        { new KeyNotFoundException("missing key"), 404, "NOT_FOUND" },
        { new UnauthorizedAccessException(), 401, "UNAUTHORIZED" },
        { new ArgumentException("bad arg"), 400, "BAD_REQUEST" },
        { new InvalidOperationException("bad state"), 400, "BAD_REQUEST" },
        { new Exception("boom"), 500, "INTERNAL_ERROR" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Maps_exception_to_status_and_error_code(Exception exception, int status, string errorCode)
    {
        var response = ExceptionMapper.Map(exception);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(errorCode, Assert.Single(response.Errors).Code);
    }

    [Fact]
    public void Client_errors_keep_their_message()
    {
        var response = ExceptionMapper.Map(new NotFoundException("Patient 5 not found"));

        Assert.Equal("Patient 5 not found", response.Message);
        Assert.Equal(ApiResponseTypes.CLIENT_ERROR, response.Types);
    }

    [Fact]
    public void Unknown_exceptions_never_leak_their_message()
    {
        var response = ExceptionMapper.Map(new Exception("connection string is Server=prod;Password=secret"));

        Assert.Equal("An unexpected error occurred", response.Message);
        Assert.Equal(ApiResponseTypes.SERVER_ERROR, response.Types);
        Assert.DoesNotContain("secret", response.Errors.Single().Message);
    }

    [Fact]
    public void Unauthorized_does_not_use_the_framework_message()
    {
        var response = ExceptionMapper.Map(new UnauthorizedAccessException());

        Assert.Equal("Unauthorized", response.Message);
    }
}