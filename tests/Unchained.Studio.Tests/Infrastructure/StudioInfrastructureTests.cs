using Unchained.Studio.Infrastructure;

namespace Unchained.Studio.Tests.Infrastructure;

/// <summary>Tests for <see cref="BusyScope" /> and <see cref="FeatureFlags" />.</summary>
public sealed class StudioInfrastructureTests
{
    [Fact]
    public void BusyScope_Constructor_NullCallback_Throws() =>
        Should.Throw<ArgumentNullException>(static () => new BusyScope(null!));

    [Fact]
    public void BusyScope_Begin_SetsMessageAndDisposeClearsIt()
    {
        var state = "initial";
        var scope = new BusyScope(msg => state = msg);

        var token = scope.Begin("Loading…");
        state.ShouldBe("Loading…");

        token.Dispose();
        state.ShouldBeNull();
    }

    [Fact]
    public void BusyScope_UsingBlock_ClearsOnScopeExit()
    {
        var transitions = new List<string?>();
        var scope = new BusyScope(transitions.Add);

        using (scope.Begin("Working"))
            transitions[^1].ShouldBe("Working");

        transitions[^1].ShouldBeNull();
    }

    [Fact]
    public void FeatureFlags_EnablePdfiumCompare_RoundTrips()
    {
        var original = FeatureFlags.EnablePdfiumCompare;
        try
        {
            FeatureFlags.EnablePdfiumCompare = true;
            FeatureFlags.EnablePdfiumCompare.ShouldBeTrue();

            FeatureFlags.EnablePdfiumCompare = false;
            FeatureFlags.EnablePdfiumCompare.ShouldBeFalse();
        }
        finally
        {
            FeatureFlags.EnablePdfiumCompare = original;
        }
    }
}
