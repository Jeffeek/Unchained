using Shouldly;
using Unchained.Xlsx.Security;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Security;

/// <summary>Tests for <see cref="SheetProtection" /> settings.</summary>
public sealed class SheetProtectionTests
{
    [Fact]
    public void New_DefaultValues_NotProtected()
    {
        var protection = new SheetProtection();

        protection.IsProtected.ShouldBeFalse();
        protection.PasswordHash.ShouldBeNull();
        protection.AllowSelectLockedCells.ShouldBeTrue();
        protection.AllowSelectUnlockedCells.ShouldBeTrue();
        protection.AllowFormatCells.ShouldBeFalse();
        protection.AllowInsertRows.ShouldBeFalse();
        protection.AllowInsertColumns.ShouldBeFalse();
        protection.AllowDeleteRows.ShouldBeFalse();
        protection.AllowDeleteColumns.ShouldBeFalse();
        protection.AllowSort.ShouldBeFalse();
        protection.AllowAutoFilter.ShouldBeFalse();
    }

    [Fact]
    public void Protect_WithoutPassword_EnablesProtection()
    {
        var protection = new SheetProtection();

        protection.Protect();

        protection.IsProtected.ShouldBeTrue();
        protection.PasswordHash.ShouldBeNull();
    }

    [Fact]
    public void Protect_WithPassword_EnablesProtectionAndStoresHash()
    {
        var protection = new SheetProtection();

        protection.Protect("password123");

        protection.IsProtected.ShouldBeTrue();
        protection.PasswordHash.ShouldNotBeNull();
        protection.PasswordHash.ShouldNotBe("password123"); // Should be hashed
    }

    [Fact]
    public void Protect_SamePassword_ProducesSameHash()
    {
        var protection1 = new SheetProtection();
        var protection2 = new SheetProtection();

        protection1.Protect("test");
        protection2.Protect("test");

        protection1.PasswordHash.ShouldBe(protection2.PasswordHash);
    }

    [Fact]
    public void Protect_DifferentPasswords_ProduceDifferentHashes()
    {
        var protection1 = new SheetProtection();
        var protection2 = new SheetProtection();

        protection1.Protect("test1");
        protection2.Protect("test2");

        protection1.PasswordHash.ShouldNotBe(protection2.PasswordHash);
    }

    [Fact]
    public void Unprotect_ClearsProtectionAndPassword()
    {
        var protection = new SheetProtection();
        protection.Protect("password");

        protection.Unprotect();

        protection.IsProtected.ShouldBeFalse();
        protection.PasswordHash.ShouldBeNull();
    }

    [Fact]
    public void AllowSelectLockedCells_CanBeSetAndGet()
    {
        var protection = new SheetProtection
        {
            AllowSelectLockedCells = false
        };

        protection.AllowSelectLockedCells.ShouldBeFalse();
    }

    [Fact]
    public void AllowSelectUnlockedCells_CanBeSetAndGet()
    {
        var protection = new SheetProtection
        {
            AllowSelectUnlockedCells = false
        };

        protection.AllowSelectUnlockedCells.ShouldBeFalse();
    }

    [Fact]
    public void PermissionFlags_CanBeModified()
    {
        var protection = new SheetProtection
        {
            AllowFormatCells = true,
            AllowInsertRows = true,
            AllowInsertColumns = true,
            AllowDeleteRows = true,
            AllowDeleteColumns = true,
            AllowSort = true,
            AllowAutoFilter = true
        };

        protection.AllowFormatCells.ShouldBeTrue();
        protection.AllowInsertRows.ShouldBeTrue();
        protection.AllowInsertColumns.ShouldBeTrue();
        protection.AllowDeleteRows.ShouldBeTrue();
        protection.AllowDeleteColumns.ShouldBeTrue();
        protection.AllowSort.ShouldBeTrue();
        protection.AllowAutoFilter.ShouldBeTrue();
    }
}
