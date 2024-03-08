using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Ewell.Contracts.Ido
{
    public partial class EwellContract
    {
        public override Address GetWhitelistContractAddress(Empty input)
        {
            return State.WhitelistContract.Value;
        }

        public override Address GetProxyAccountContract(Empty input)
        {
            return State.ProxyAccountContract.Value;
        }
        
        public override Address GetTokenContractAddress(Empty input)
        {
            return State.TokenContract.Value;
        }

        public override Hash GetWhitelistId(Hash input)
        {
            return State.WhiteListIdMap[input];
        }

        public override ProjectInfo GetProjectInfo(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            var liquidatedDamageProportionInfo = projectInfo.LiquidatedDamageProportion;
            //to adapt previously created projects
            if (liquidatedDamageProportionInfo == null)
            {
                projectInfo.LiquidatedDamageProportion =
                    Extensions.CreateProportionInfo(EwellContractConstants.DefaultLiquidatedDamageProportion);
            }
            return projectInfo;
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
        
        public override LiquidatedDamageConfig GetLiquidatedDamageConfig(Empty input)
        {
            return State.LiquidatedDamageConfig.Value;
        }
    }
}