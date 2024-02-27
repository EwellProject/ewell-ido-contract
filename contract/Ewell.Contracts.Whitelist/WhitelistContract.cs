using Google.Protobuf.WellKnownTypes;

namespace Ewell.Contracts.Whitelist;

public partial class WhitelistContract : WhitelistContractContainer.WhitelistContractBase
{
    public override Empty Initialize(Empty input)
    {
        return new Empty();
    }
}