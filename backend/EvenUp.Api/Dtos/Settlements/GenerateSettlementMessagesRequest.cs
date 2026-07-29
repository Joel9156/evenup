namespace EvenUp.Api.Dtos.Settlements;

public class GenerateSettlementMessagesRequest
{
    public List<AccountOverride> AccountOverrides { get; set; } = [];
}
