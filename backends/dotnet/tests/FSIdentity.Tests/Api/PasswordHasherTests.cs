using FSIdentity.Domain.Exceptions;
using FSIdentity.Identity.Auth;
using Microsoft.Extensions.Options;

namespace FSIdentity.Tests.Api;

public class PasswordHasherTests
{
    private static PasswordHasher Make(int minLength = 8)
        => new(Options.Create(new PasswordOptions { WorkFactor = 4, MinLength = minLength }));

    [Fact]
    public void A_hash_verifies_only_the_password_it_came_from_and_is_salted()
    {
        var hasher = Make();

        var one = hasher.Hash("Passw0rd!");
        var two = hasher.Hash("Passw0rd!");

        Assert.True(hasher.Verify("Passw0rd!", one));
        Assert.False(hasher.Verify("passw0rd!", one));
        Assert.NotEqual(one, two);
        Assert.StartsWith("$2", one);
    }

    [Fact]
    public void Something_that_is_not_a_hash_never_verifies()
    {
        Assert.False(Make().Verify("anything1", "plain text, not a hash"));
        Assert.False(Make().Verify("anything1", ""));
    }

    [Theory]
    [InlineData("short1", "at least 8 characters")]
    [InlineData("longenoughbutnodigit", "a digit")]
    [InlineData("12345678901", "a letter")]
    public void A_weak_password_is_refused_with_what_it_lacks(string password, string expected)
    {
        var refused = Assert.Throws<BadRequestException>(() => Make().EnsureAcceptable(password));

        Assert.Contains(expected, refused.Message);
    }

    [Fact]
    public void A_password_with_a_letter_a_digit_and_enough_length_is_accepted()
    {
        Make().EnsureAcceptable("Abcdefg1");
        Make(minLength: 12).EnsureAcceptable("Abcdefghijk1");
        Assert.Throws<BadRequestException>(() => Make(minLength: 12).EnsureAcceptable("Abcdefg1"));
    }

    [Fact]
    public void Burning_time_does_not_throw()
    {
        Make().BurnTime();
    }
}
