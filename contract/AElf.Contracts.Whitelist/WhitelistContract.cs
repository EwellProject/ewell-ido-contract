using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Whitelist
{
    public partial class WhitelistContract : WhitelistContractContainer.WhitelistContractBase
    {
        public override Hash CreateWhitelist(CreateWhitelistInput input)
        {
            return HashHelper.ComputeFrom(0);
        }

        public override Empty AddAddressInfoListToWhitelist(AddAddressInfoListToWhitelistInput input)
        {
            return new Empty();
        }

        public override Empty RemoveAddressInfoListFromWhitelist(RemoveAddressInfoListFromWhitelistInput input)
        {
            return new Empty();
        }

        public override Empty EnableWhitelist(Hash input)
        {
            return new Empty();
        }
        

        public override Empty DisableWhitelist(Hash input)
        {
            return new Empty();
        }

        public override BoolValue GetAddressFromWhitelist(GetAddressFromWhitelistInput input)
        {
            return new BoolValue()
            {
                Value = true
            };
        }

        public override WhitelistIdList GetWhitelistByProject(Hash input)
        {
            return new WhitelistIdList()
            {
                WhitelistId = {HashHelper.ComputeFrom(0)}
            };
        }

        public override WhitelistInfo GetWhitelist(Hash input)
        {
            return new WhitelistInfo()
            {
                IsAvailable = true
            };
        }
    }


}