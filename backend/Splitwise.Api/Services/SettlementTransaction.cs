namespace Splitwise.Api.Services;

public record SettlementTransaction(Guid FromMemberId, Guid ToMemberId, decimal Amount);
