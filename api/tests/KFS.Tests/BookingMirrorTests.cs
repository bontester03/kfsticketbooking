using FluentAssertions;
using KFS.Application.Services;
using KFS.Domain.Enums;
using Xunit;

namespace KFS.Tests;

public class BookingMirrorTests
{
    [Theory]
    [InlineData(ZoneCode.VIPAF, ZoneCode.VIPAM)]
    [InlineData(ZoneCode.VIPAM, ZoneCode.VIPAF)]
    [InlineData(ZoneCode.VIPBF, ZoneCode.VIPBM)]
    [InlineData(ZoneCode.VIPBM, ZoneCode.VIPBF)]
    public void Mirror_zone_code_pairs_within_group(ZoneCode input, ZoneCode expected)
        => BookingService.MirrorZoneCode(input).Should().Be(expected);

    [Theory]
    [InlineData(ZoneGroup.A, ZoneSide.Female, ZoneCode.VIPAF)]
    [InlineData(ZoneGroup.A, ZoneSide.Male,   ZoneCode.VIPAM)]
    [InlineData(ZoneGroup.B, ZoneSide.Female, ZoneCode.VIPBF)]
    [InlineData(ZoneGroup.B, ZoneSide.Male,   ZoneCode.VIPBM)]
    public void Zone_code_resolves_from_group_and_side(ZoneGroup g, ZoneSide s, ZoneCode expected)
        => BookingService.ZoneCodeFor(g, s).Should().Be(expected);
}

public class StudentInitialPasswordTests
{
    [Theory]
    [InlineData("Safa", 2010, 3, 15, "Saf15032010")]
    [InlineData("Layan", 2009, 6, 22, "Lay22062009")]
    [InlineData("Yousef", 2010, 11, 4, "You04112010")]
    public void Computes_documented_format(string firstName, int y, int m, int d, string expected)
        => StudentService.ComputeInitialPassword(firstName, new DateTime(y, m, d))
            .Should().Be(expected);

    [Fact]
    public void Pads_short_first_names_to_three_chars()
        => StudentService.ComputeInitialPassword("Al", new DateTime(2010, 1, 2))
            .Should().Be("Alx02012010");
}
