using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Ewell
{
    public partial class EwellContract
    {
        public override Address GetWhitelistContractAddress(Empty input)
        {
            return State.WhitelistContract.Value;
        }

        public override Address GetTokenAddress(Empty input)
        {
            return State.TokenContract.Value;
        }

        public override Hash GetWhitelistId(Hash input)
        {
            return State.WhiteListIdMap[input];
        }

        public override ProjectInfo GetProjectInfo(Hash input)
        {
            ValidProjectExist(input);
            return State.ProjectInfoMap[input];
        }

        public override InvestDetail GetInvestDetail(GetInvestDetailInput input)
        {
            ValidProjectExist(input.ProjectId);
            return State.InvestDetailMap[input.ProjectId][input.User];
        }

        public override ProfitDetail GetProfitDetail(GetProfitDetailInput input)
        {
            ValidProjectExist(input.ProjectId);
            return State.ProfitDetailMap[input.ProjectId][input.User];
        }

        public override ProjectListInfo GetProjectListInfo(Hash input)
        {
            ValidProjectExist(input);
            return State.ProjectListInfoMap[input];
        }

        public override Address GetAdmin(Empty input)
        {
            return State.Admin.Value;
        }

        public override LiquidatedDamageDetails GetLiquidatedDamageDetails(Hash input)
        {
            ValidProjectExist(input);
            return State.LiquidatedDamageDetailsMap[input];
        }

        public override Address GetProjectAddressByProjectHash(Hash input)
        {
            ValidProjectExist(input);
            return State.ProjectAddressMap[input];
        }

        public override Address GetPendingProjectAddress(Address input)
        {
            var hash = GetProjectVirtualAddressHash(input);
            var virtualAddress = Context.ConvertVirtualAddressToContractAddress(hash);
            return virtualAddress;
        }
    }
}