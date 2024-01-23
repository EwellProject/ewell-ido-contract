using AElf.Types;

namespace Ewell.Contracts.Ido;

public static class Extensions
{
    public static ProjectInfo CreateProjectInfo(RegisterInput input, Hash projectId, Address creator,
        long toRaisedAmount, Hash virtualAddressHash)
    {
        return new ProjectInfo
        {
            ProjectId = projectId,
            AcceptedCurrency = input.AcceptedCurrency,
            ProjectCurrency = input.ProjectCurrency,
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
            ToRaisedAmount = toRaisedAmount,
            Enabled = true,
            VirtualAddressHash = virtualAddressHash,
            TokenReleaseTime = input.TokenReleaseTime
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
            RestDistributeProportion = input.RestDistributeProportion,
            PeriodDuration = input.PeriodDuration
        };
    }

    public static ProjectRegistered GenerateProjectRegisteredEvent(RegisterInput input, Hash projectId, Address creator,
        long toRaisedAmount)
    {
        return new ProjectRegistered()
        {
            ProjectId = projectId,
            AcceptedCurrency = input.AcceptedCurrency,
            ProjectCurrency = input.ProjectCurrency,
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
            ToRaisedAmount = toRaisedAmount,
            Creator = creator,
            FirstDistributeProportion = input.FirstDistributeProportion,
            RestDistributeProportion = input.RestDistributeProportion,
            PeriodDuration = input.PeriodDuration,
            TokenReleaseTime = input.TokenReleaseTime
        };
    }
}