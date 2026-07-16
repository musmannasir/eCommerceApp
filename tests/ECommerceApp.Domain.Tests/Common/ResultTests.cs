using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_result_has_no_errors()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_result_carries_the_given_error()
    {
        var error = Error.Validation("field.required", "Field is required.");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(error);
        result.FirstError.Should().Be(error);
    }

    [Fact]
    public void Generic_success_result_exposes_its_value()
    {
        Result<int> result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Accessing_value_of_a_failed_result_throws()
    {
        var result = Result.Failure<int>(Error.NotFound("x", "not found"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Implicit_conversion_from_value_creates_a_success_result()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Constructing_a_success_result_with_errors_is_not_allowed()
    {
        var act = () => Result.Failure(Array.Empty<Error>());

        act.Should().Throw<InvalidOperationException>();
    }
}
