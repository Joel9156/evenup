namespace EvenUp.Api.Services;

// Greedy minimum-cash-flow settlement (see specs/evenup-clone-spec.md section 6).
//
// Not guaranteed to hit the theoretical optimum (n-1 transactions for n people with a
// nonzero balance — finding that requires NP-hard subset-sum search), but it's O(n log n)
// and gets very close in practice: every step fully zeroes out at least one person's
// balance, so it can never take more than n-1 transactions either.
//
// Pure and stateless — no DB access, so it's cheap and deterministic to unit test directly.
public class SettlementCalculator
{
    public List<SettlementTransaction> Calculate(IReadOnlyDictionary<Guid, decimal> netBalances)
    {
        var creditors = netBalances
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Select(x => (x.Key, x.Value))
            .ToList();

        var debtors = netBalances
            .Where(x => x.Value < 0)
            .OrderBy(x => x.Value) // largest debt first (most negative)
            .Select(x => (x.Key, x.Value))
            .ToList();

        var transactions = new List<SettlementTransaction>();
        var ci = 0;
        var di = 0;

        while (ci < creditors.Count && di < debtors.Count)
        {
            var (creditorId, creditorAmount) = creditors[ci];
            var (debtorId, debtorAmount) = debtors[di];

            // decimal arithmetic is exact base-10, unlike float/double — so subtracting this
            // exact amount from the smaller side always lands on precisely 0, never a residue
            // like 0.0000000001, which is what makes the "== 0" advance check below safe.
            var amount = Math.Min(creditorAmount, -debtorAmount);

            transactions.Add(new SettlementTransaction(debtorId, creditorId, amount));

            creditors[ci] = (creditorId, creditorAmount - amount);
            debtors[di] = (debtorId, debtorAmount + amount);

            if (creditors[ci].Value == 0)
            {
                ci++;
            }

            if (debtors[di].Value == 0)
            {
                di++;
            }
        }

        return transactions;
    }
}
