using AElf.Types;

namespace Ewell.Contracts.Ido;

public static class Extensions
{
    public static ProjectInfo CreateProjectInfo(RegisterInput input, Hash projectId, Address creator,
        long targetRaisedAmount, Hash virtualAddressHash, int liquidatedDamageProportion)
    {
        return new ProjectInfo
        {
            ProjectId = projectId,
            AcceptedSymbol = input.AcceptedSymbol,
            ProjectSymbol = input.ProjectSymbol,
            CrowdFundingType = input.CrowdFundingType,
            CrowdFundingIssueAmount = input.CrowdFundingIssueAmount,
            PreSalePrice = input.PreSalePrice,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            MinSubscription = input.MinSubscription,
            MaxSubscription = input.MaxSubscription,
            IsBurnRestToken = input.IsBurnRestToken,
            AdditionalInfo = input.AdditionalInfo,
            Creator = creator,
            TargetRaisedAmount = targetRaisedAmount,
            Enabled = true,
            VirtualAddressHash = virtualAddressHash,
            TokenReleaseTime = input.TokenReleaseTime,
            LiquidatedDamageProportion = CreateProportionInfo(liquidatedDamageProportion)
        };
    }
    
    public static ProjectListInfo CreateProjectListInfo(RegisterInput input, Hash projectId)
    {
        return new ProjectListInfo
        {
            ProjectId = projectId,
            PublicSalePrice = input.PublicSalePrice,
            LiquidityLockProportion = input.LiquidityLockProportion,
            ListMarketInfo = input.ListMarketInfo,
            UnlockTime = input.UnlockTime,
            LatestPeriod = 0,
            TotalPeriod = input.TotalPeriod,
            FirstDistributeProportion = input.FirstDistributeProportion,
            RestPeriodDistributeProportion = input.RestPeriodDistributeProportion,
            PeriodDuration = input.PeriodDuration
        };
    }

    public static ProjectRegistered GenerateProjectRegisteredEvent(RegisterInput input, Address virtualAddress, ProjectInfo projectInfo)
    {
        return new ProjectRegistered()
        {
            ProjectId = projectInfo.ProjectId,
            AcceptedSymbol = input.AcceptedSymbol,
            ProjectSymbol = input.ProjectSymbol,
            CrowdFundingType = input.CrowdFundingType,
            CrowdFundingIssueAmount = input.CrowdFundingIssueAmount,
            PreSalePrice = input.PreSalePrice,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            MinSubscription = input.MinSubscription,
            MaxSubscription = input.MaxSubscription,
            PublicSalePrice = input.PublicSalePrice,
            ListMarketInfo = input.ListMarketInfo,
            LiquidityLockProportion = input.LiquidityLockProportion,
            UnlockTime = input.UnlockTime,
            IsEnableWhitelist = input.IsEnableWhitelist,
            WhitelistId = input.WhitelistId,
            IsBurnRestToken = input.IsBurnRestToken,
            TotalPeriod = input.TotalPeriod,
            AdditionalInfo = input.AdditionalInfo,
            TargetRaisedAmount = projectInfo.TargetRaisedAmount,
            Creator = projectInfo.Creator,
            FirstDistributeProportion = input.FirstDistributeProportion,
            RestPeriodDistributeProportion = input.RestPeriodDistributeProportion,
            PeriodDuration = input.PeriodDuration,
            TokenReleaseTime = input.TokenReleaseTime,
            VirtualAddress = virtualAddress,
            LiquidatedDamageProportion = projectInfo.LiquidatedDamageProportion.Value
        };
    }

    public static ProportionInfo CreateProportionInfo(int liquidatedDamageProportion)
    {
        return new ProportionInfo
        {
            Value = liquidatedDamageProportion
        };
    }
}