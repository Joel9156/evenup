using EvenUp.Api.Services;
using Xunit;

namespace EvenUp.Api.Tests.Services;

public class SettlementCalculatorTests
{
    private readonly SettlementCalculator _sut = new();

    [Fact]
    public void Calculate_AllBalancesZero_ReturnsNoTransactions()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        var result = _sut.Calculate(new Dictionary<Guid, decimal> { [alice] = 0m, [bob] = 0m });

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_EmptyInput_ReturnsNoTransactions()
    {
        var result = _sut.Calculate(new Dictionary<Guid, decimal>());

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_TwoPeople_OneOwesTheOther_ProducesSingleTransaction()
    {
        var alice = Guid.NewGuid(); // paid, is owed
        var bob = Guid.NewGuid();   // owes

        var result = _sut.Calculate(new Dictionary<Guid, decimal> { [alice] = 50m, [bob] = -50m });

        var transaction = Assert.Single(result);
        Assert.Equal(bob, transaction.FromMemberId);
        Assert.Equal(alice, transaction.ToMemberId);
        Assert.Equal(50m, transaction.Amount);
    }

    [Fact]
    public void Calculate_ThreePeople_EvenDinnerSplit_ProducesMinimumTwoTransactions()
    {
        // Alice paid $90 for dinner, split evenly three ways: net +60 / -30 / -30.
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();

        var result = _sut.Calculate(new Dictionary<Guid, decimal>
        {
            [alice] = 60m,
            [bob] = -30m,
            [carol] = -30m,
        });

        Assert.Equal(2, result.Count); // n-1 for n=3 people with nonzero balance — the algorithm's minimum here
        Assert.All(result, t => Assert.Equal(alice, t.ToMemberId));
        Assert.Equal(60m, result.Sum(t => t.Amount));
    }

    [Fact]
    public void Calculate_NeverProducesATransactionWhereFromEqualsTo()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();

        var result = _sut.Calculate(new Dictionary<Guid, decimal>
        {
            [a] = 40m,
            [b] = 25m,
            [c] = -10m,
            [d] = -55m,
        });

        Assert.All(result, t => Assert.NotEqual(t.FromMemberId, t.ToMemberId));
    }

    [Fact]
    public void Calculate_TotalAmountTransferred_EqualsSumOfPositiveBalances()
    {
        var netBalances = new Dictionary<Guid, decimal>
        {
            [Guid.NewGuid()] = 100m,
            [Guid.NewGuid()] = 20m,
            [Guid.NewGuid()] = -70m,
            [Guid.NewGuid()] = -50m,
        };

        var result = _sut.Calculate(netBalances);

        var expectedTotal = netBalances.Values.Where(v => v > 0).Sum();
        Assert.Equal(expectedTotal, result.Sum(t => t.Amount));
    }

    [Fact]
    public void Calculate_ApplyingResultingTransactions_ZeroesOutEveryBalance()
    {
        // Correctness invariant: whatever the transaction list is, replaying it against the
        // original balances (credit the "to" side, debit the "from" side) must bring every
        // member's balance to exactly zero — this is the actual definition of "settled."
        var netBalances = new Dictionary<Guid, decimal>
        {
            [Guid.NewGuid()] = 33.33m,
            [Guid.NewGuid()] = 33.33m,
            [Guid.NewGuid()] = 33.34m,
            [Guid.NewGuid()] = -100m,
        };

        var result = _sut.Calculate(netBalances);

        var replay = new Dictionary<Guid, decimal>(netBalances);
        foreach (var t in result)
        {
            replay[t.FromMemberId] += t.Amount;
            replay[t.ToMemberId] -= t.Amount;
        }

        Assert.All(replay.Values, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void Calculate_NeverProducesMoreThanNMinusOneTransactions()
    {
        // n distinct nonzero balances -> at most n-1 transactions, since every step zeroes
        // out at least one person entirely.
        var netBalances = new Dictionary<Guid, decimal>
        {
            [Guid.NewGuid()] = 10m,
            [Guid.NewGuid()] = 20m,
            [Guid.NewGuid()] = 30m,
            [Guid.NewGuid()] = -15m,
            [Guid.NewGuid()] = -20m,
            [Guid.NewGuid()] = -25m,
        };

        var result = _sut.Calculate(netBalances);

        Assert.True(result.Count <= netBalances.Count - 1);
    }
}
