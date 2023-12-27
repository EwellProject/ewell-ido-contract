using Google.Protobuf.WellKnownTypes;

namespace Awaken.Contracts.Token
{
    public partial class TokenContract : TokenContractContainer.TokenContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.Owner.Value == null, "Already initialized.");
            State.Owner.Value = input.Owner;
            State.MinterMap[input.Owner] = true;
            return new Empty();
        }
    }
}