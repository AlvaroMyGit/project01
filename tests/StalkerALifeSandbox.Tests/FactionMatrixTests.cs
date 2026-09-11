using StalkerALifeSandbox.Factions;

namespace StalkerALifeSandbox.Tests;

public class FactionMatrixTests
{
    [Fact]
    public void Set_KeepsRelationSymmetric()
    {
        var m = new FactionMatrix();
        m.Set("Duty", "Freedom", FactionRelation.War);

        Assert.Equal(FactionRelation.War, m.Get("Duty", "Freedom"));
        Assert.Equal(FactionRelation.War, m.Get("Freedom", "Duty"));
    }

    [Fact]
    public void Get_UnknownFaction_ReturnsNeutral()
    {
        var m = new FactionMatrix();

        Assert.Equal(FactionRelation.Neutral, m.Get("Loner", "NotAFaction"));
        Assert.Equal(FactionRelation.Neutral, m.Get("Nope", "AlsoNope"));
    }

    [Theory]
    [InlineData(FactionRelation.War, true)]
    [InlineData(FactionRelation.Hostile, true)]
    [InlineData(FactionRelation.Neutral, false)]
    [InlineData(FactionRelation.Friendly, false)]
    [InlineData(FactionRelation.Allied, false)]
    public void AreHostile_TrueOnlyAtOrBelowHostile(FactionRelation rel, bool expected)
    {
        var m = new FactionMatrix();
        m.Set("Loner", "Bandit", rel);

        Assert.Equal(expected, m.AreHostile("Loner", "Bandit"));
    }

    [Theory]
    [InlineData(FactionRelation.Allied, true)]
    [InlineData(FactionRelation.Friendly, true)]
    [InlineData(FactionRelation.Neutral, false)]
    [InlineData(FactionRelation.Hostile, false)]
    public void AreFriendly_TrueOnlyAtOrAboveFriendly(FactionRelation rel, bool expected)
    {
        var m = new FactionMatrix();
        m.Set("Duty", "Ecologist", rel);

        Assert.Equal(expected, m.AreFriendly("Duty", "Ecologist"));
    }

    [Fact]
    public void IndexOf_MapsKnownFactions_AndRejectsUnknown()
    {
        var m = new FactionMatrix();

        Assert.Equal(0, m.IndexOf("Loner"));
        Assert.Equal(-1, m.IndexOf("Ghost"));
    }

    [Fact]
    public void FactionIds_MatchDeclaredCount()
    {
        Assert.Equal(FactionMatrix.FactionCount, FactionMatrix.FactionIds.Length);
    }
}
