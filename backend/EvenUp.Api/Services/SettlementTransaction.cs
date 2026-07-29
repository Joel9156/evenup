namespace EvenUp.Api.Services;

public record SettlementTransaction(Guid FromMemberId, Guid ToMemberId, decimal Amount);
