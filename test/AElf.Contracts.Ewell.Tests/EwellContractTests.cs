using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using System.Threading.Tasks;
using Awaken.Contracts.Swap;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using System;
using System.Linq;
using System.Threading;
using AElf.Contracts.Ewell.Tests;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Shouldly;
using Awaken.Contracts.Token;
using Google.Protobuf.Collections;
using Xunit.Sdk;

namespace AElf.Contracts.Ewell
{
    public class EwellContractTests : EwellContractTestBase
    {
        private Hash projectId0;
        [Fact]
        public async Task InitializeTest()
        {
           await Initialize();
           await AdminStub.Initialize.SendAsync(new InitializeInput()
           {
               WhitelistContract = WhitelistContractAddress
           });
           var whitelistAddress = await AdminStub.GetWhitelistContractAddress.CallAsync(new Empty());
           whitelistAddress.ShouldNotBe(new Address());
           var virtualAddress = await AdminStub.GetPendingProjectAddress.CallAsync(AdminAddress);
           await TokenContractStub.Transfer.SendAsync(new AElf.Contracts.MultiToken.TransferInput()
           {
               Amount = 1_00000000,
               Symbol = "TEST",
               Memo = "ForUserClaim",
               To = virtualAddress
           });
        }

        [Fact]
        public async Task RegisterTest()
        {
            await InitializeTest();
            var registerInput = new RegisterInput()
            {
                AcceptedCurrency = "ELF",
                ProjectCurrency = "TEST",
                CrowdFundingType = "标价销售",
                CrowdFundingIssueAmount = 1_00000000,
                PreSalePrice = 1_00000000,
                StartTime = blockTimeProvider.GetBlockTime().AddSeconds(3),
                EndTime = blockTimeProvider.GetBlockTime().AddSeconds(30),
                MinSubscription = 10,
                MaxSubscription = 100,
                IsEnableWhitelist = false,
                IsBurnRestToken = true,
                AdditionalInfo = new AdditionalInfo(),
                PublicSalePrice = 2_000000000,
                LiquidityLockProportion = 50,
                ListMarketInfo = new ListMarketInfo()
                {
                    Data = { new ListMarket()
                    {
                        Market = AwakenSwapContractAddress,
                        Weight = 100
                    }}
                },
                UnlockTime = Timestamp.FromDateTime(DateTime.UtcNow.Add(new TimeSpan(0, 0, 30000))),
                TotalPeriod = 1,
                FirstDistributeProportion = 100_000000,
                RestDistributeProportion = 0,
                PeriodDuration = 0
            };
           var executionResult = await AdminStub.Register.SendAsync(registerInput);

           var projectId = ProjectRegistered.Parser
               .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(ProjectRegistered))).NonIndexed)
               .ProjectId;

          var whitelistId =  await AdminStub.GetWhitelistId.CallAsync(projectId);
          whitelistId.ShouldBe(HashHelper.ComputeFrom(0));
          projectId0 = projectId;

        }

        [Fact]
        public async Task WhitelistTest()
        {
            await RegisterTest();
        
            await AdminStub.AddWhitelists.SendAsync(new AddWhitelistsInput()
            {
                ProjectId = projectId0,
                Users = {UserTomAddress}
            });
            
            await AdminStub.RemoveWhitelists.SendAsync(new RemoveWhitelistsInput()
            {
                ProjectId = projectId0,
                Users = {UserTomAddress}
            });

  
        }

        [Fact]
        public async Task InvestTest()
        {
            var investAmount = 100;
            await RegisterTest();
          
            
            blockTimeProvider.SetBlockTime(blockTimeProvider.GetBlockTime().AddSeconds(3));
            await TomTokenContractStub.Approve.SendAsync(new MultiToken.ApproveInput()
            {
                Spender = EwellContractAddress,
                Amount = 10000,
                Symbol = "ELF"
            });
            await TomStub.Invest.SendAsync(new InvestInput()
            {
                ProjectId = projectId0,
                Currency = "ELF",
                InvestAmount = investAmount

            });

            var investDetail =  await TomStub.GetInvestDetail.CallAsync(new GetInvestDetailInput()
            {
                ProjectId = projectId0,
                User = UserTomAddress
            });
            investDetail.Amount.ShouldBe(investAmount);
            
         
        }

        [Fact]
        public async Task ClaimTest()
        {
            await InvestTest();
            blockTimeProvider.SetBlockTime(blockTimeProvider.GetBlockTime().AddSeconds(30));
            await AdminStub.NextPeriod.SendAsync(projectId0);
          
            await TomStub.Claim.SendAsync(new ClaimInput()
            {
                ProjectId = projectId0,
                User = UserTomAddress
            });
            var balance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "TEST"
            });
            balance.Balance.ShouldNotBe(0);
        }

        [Fact]
        public async Task CancelTest()
        {
            await InvestTest();
            var projectInfoBefore = await AdminStub.GetProjectInfo.CallAsync(projectId0);
            projectInfoBefore.Enabled.ShouldBeTrue();
            var virtualAddress = await AdminStub.GetProjectAddressByProjectHash.CallAsync(projectId0);
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = virtualAddress,
                Symbol = "TEST"
            });
            
            await AdminStub.Cancel.SendAsync(projectId0);
            
            var projectInfoAfter = await AdminStub.GetProjectInfo.CallAsync(projectId0);
            projectInfoAfter.Enabled.ShouldBeFalse();
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = virtualAddress,
                Symbol = "TEST"
            });
            balanceBefore.Balance.ShouldBePositive();
            balanceAfter.Balance.ShouldBe(0);
        }
        [Fact]
        public async Task ClaimLiquidatedDamageTest()
        {
            await UnInvestTest();
            await AdminStub.Cancel.SendAsync(projectId0);
            
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            await TomStub.ClaimLiquidatedDamage.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
            var liquidatedDamageDetails = await  TomStub.GetLiquidatedDamageDetails.CallAsync(projectId0);
            var liquidatedDamage =  liquidatedDamageDetails.Details.First(x => x.User == UserTomAddress);
            liquidatedDamage.Claimed.ShouldBe(true);
        }

        [Fact]
        public async Task UnInvestTest()
        {
            await InvestTest();
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });

            await TomStub.UnInvest.SendAsync(projectId0);
            
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
            var profit = await TomStub.GetProfitDetail.CallAsync(new GetProfitDetailInput()
            {
                ProjectId = projectId0,
                User = UserTomAddress
            });
            profit.TotalProfit.ShouldBe(0);
            
            //User has already unInvest
            var alreadyUnInvestException = await TomStub.UnInvest.SendWithExceptionAsync(projectId0);
            alreadyUnInvestException.TransactionResult.Error.ShouldContain("User has already unInvest");
        }

        
        // [Fact]
        // public async Task AddLiquidityTest()
        // {
        //     await InvestTest();
        //     blockTimeProvider.SetBlockTime(blockTimeProvider.GetBlockTime().AddSeconds(30));
        //     await TokenContractStub.Transfer.SendAsync(new AElf.Contracts.MultiToken.TransferInput()
        //     {
        //         Amount = 100000000000,
        //         Symbol = "TEST",
        //         Memo = "ForAddLiquidity",
        //         To = EwellContractAddress
        //     });
        //     await AdminStub.LockLiquidity.SendAsync(projectId0);
        //     
        // }
        
        [Fact]
        public async Task RefundTest()
        {
            await InvestTest();
            await AdminStub.Cancel.SendAsync(projectId0);
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            
            await TomStub.ReFund.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
          
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
            var profit = await TomStub.GetProfitDetail.CallAsync(new GetProfitDetailInput()
            {
                ProjectId = projectId0,
                User = UserTomAddress
            });
            profit.TotalProfit.ShouldBe(0);
        }

        [Fact]
        public async Task RefundAllTest()
        {
            await InvestTest();
            await AdminStub.Cancel.SendAsync(projectId0);
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            
            await AdminStub.ReFundAll.SendAsync(new ReFundAllInput()
            {
                ProjectId = projectId0,
                Users = { UserTomAddress}
            });
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
          
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
            var profit = await TomStub.GetProfitDetail.CallAsync(new GetProfitDetailInput()
            {
                ProjectId = projectId0,
                User = UserTomAddress
            });
            profit.TotalProfit.ShouldBe(0);
        }
        
        [Fact]
        public async Task ClaimLiquidatedDamageAllTest()
        {
            await UnInvestTest();
            await AdminStub.Cancel.SendAsync(projectId0);
            
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            await AdminStub.ClaimLiquidatedDamageAll.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
            var liquidatedDamageDetails = await  TomStub.GetLiquidatedDamageDetails.CallAsync(projectId0);
            var liquidatedDamage =  liquidatedDamageDetails.Details.First(x => x.User == UserTomAddress);
            liquidatedDamage.Claimed.ShouldBe(true);
        }
        
        
        [Fact]
        public async Task WithdrawTest()
        {
            await InvestTest();
            
            blockTimeProvider.SetBlockTime(blockTimeProvider.GetBlockTime().AddSeconds(30));
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = AdminAddress,
                Symbol = "ELF"
            });
            await AdminStub.Withdraw.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = AdminAddress,
                Symbol = "ELF"
            });
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
        }

        [Fact]
        public async Task GetPendingProjectAddressTest()
        {
            await InitializeTest();
            var virtualAddressExpect = await AdminStub.GetPendingProjectAddress.CallAsync(AdminAddress);
            var registerInput = new RegisterInput()
            {
                AcceptedCurrency = "ELF",
                ProjectCurrency = "TEST",
                CrowdFundingType = "标价销售",
                CrowdFundingIssueAmount = 1_00000000,
                PreSalePrice = 1_00000000,
                StartTime = blockTimeProvider.GetBlockTime().AddSeconds(3),
                EndTime = blockTimeProvider.GetBlockTime().AddSeconds(30),
                MinSubscription = 10,
                MaxSubscription = 100,
                IsEnableWhitelist = false,
                IsBurnRestToken = true,
                AdditionalInfo = new AdditionalInfo(),
                PublicSalePrice = 2_000000000,
                LiquidityLockProportion = 50,
                ListMarketInfo = new ListMarketInfo()
                {
                    Data = { new ListMarket()
                    {
                        Market = AwakenSwapContractAddress,
                        Weight = 100
                    }}
                },
                UnlockTime = Timestamp.FromDateTime(DateTime.UtcNow.Add(new TimeSpan(0, 0, 30000))),
                TotalPeriod = 1,
                FirstDistributeProportion = 100_000000,
                RestDistributeProportion = 0,
                PeriodDuration = 0
            };
            
            var executionResult = await AdminStub.Register.SendAsync(registerInput);

            var projectId = ProjectRegistered.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(ProjectRegistered))).NonIndexed)
                .ProjectId;

            var whitelistId =  await AdminStub.GetWhitelistId.CallAsync(projectId);
            whitelistId.ShouldBe(HashHelper.ComputeFrom(0));
            projectId0 = projectId;
            
             var virtualAddress = await AdminStub.GetProjectAddressByProjectHash.CallAsync(projectId0);
             virtualAddress.ShouldBe(virtualAddressExpect);
        }

        
        private async Task CreateAndGetToken()
        {
            //TEST
            var result = await TokenContractStub.Create.SendAsync(new AElf.Contracts.MultiToken.CreateInput
            {
                Issuer = AdminAddress,
                Symbol = "TEST",
                Decimals = 8,
                IsBurnable = true,
                TokenName = "TEST symbol",
                TotalSupply = 100000000_00000000
            });

            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var issueResult = await TokenContractStub.Issue.SendAsync(new AElf.Contracts.MultiToken.IssueInput
            {
                Amount = 100000000000000,
                Symbol = "TEST",
                To = AdminAddress
            });
            issueResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var balance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = AdminAddress,
                Symbol = "TEST"
            });
            balance.Balance.ShouldBe(100000000000000);
            //DAI
            var result2 = await TokenContractStub.Create.SendAsync(new AElf.Contracts.MultiToken.CreateInput
            {
                Issuer = AdminAddress,
                Symbol = "DAI",
                Decimals = 10,
                IsBurnable = true,
                TokenName = "DAI symbol",
                TotalSupply = 100000000_00000000
            });

            result2.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var issueResult2 = await TokenContractStub.Issue.SendAsync(new AElf.Contracts.MultiToken.IssueInput
            {
                Amount = 100000000000000,
                Symbol = "DAI",
                To = AdminAddress
            });
            issueResult2.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var balance2 = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput()
            {
                Owner = AdminAddress,
                Symbol = "DAI"
            });
            balance2.Balance.ShouldBe(100000000000000);
            await TokenContractStub.Transfer.SendAsync(new AElf.Contracts.MultiToken.TransferInput()
            {
                Amount = 100000000000,
                Symbol = "ELF",
                Memo = "Recharge",
                To = UserTomAddress
            });
            await TokenContractStub.Transfer.SendAsync(new AElf.Contracts.MultiToken.TransferInput()
            {
                Amount = 100000000000,
                Symbol = "ELF",
                Memo = "Recharge",
                To = UserLilyAddress
            });
        
           
            await TokenContractStub.Transfer.SendAsync(new AElf.Contracts.MultiToken.TransferInput()
            {
                Amount = 100000000000,
                Symbol = "DAI",
                Memo = "Recharge",
                To = UserTomAddress
            });
            //authorize  Tom and Lily and admin to transfer ELF and TEST and DAI to FinanceContract
            // await UserTomTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput()
            // {
            //     Amount = 100000000000,
            //     Spender = AwakenSwapContractAddress,
            //     Symbol = "ELF"
            // });
            // await UserTomTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput()
            // {
            //     Amount = 100000000000,
            //     Spender = AwakenSwapContractAddress,
            //     Symbol = "DAI"
            // });
            // await TokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput()
            // {
            //     Amount = 100000000000,
            //     Spender = AwakenSwapContractAddress,
            //     Symbol = "ELF"
            // });
            // await UserLilyTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput()
            // {
            //     Amount = 100000000000,
            //     Spender = AwakenSwapContractAddress,
            //     Symbol = "ELF"
            // });
            // await UserTomTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput()
            // {
            //     Amount = 100000000000,
            //     Spender = AwakenSwapContractAddress,
            //     Symbol = "TEST"
            // });
            // await TokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput()
            // {
            //     Amount = 100000000000,
            //     Spender = AwakenSwapContractAddress,
            //     Symbol = "TEST"
            // });
            // await UserLilyTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput()
            // {
            //     Amount = 100000000000,
            //     Spender = AwakenSwapContractAddress,
            //     Symbol = "TEST"
            // });
        }
        private async Task Initialize()
        {
            await CreateAndGetToken();
            await AdminLpStub.Initialize.SendAsync(new Awaken.Contracts.Token.InitializeInput()
            {
                Owner = AwakenSwapContractAddress
            });
            await AwakenSwapContractStub.Initialize.SendAsync(new Awaken.Contracts.Swap.InitializeInput()
            {
                Admin = AdminAddress,
                AwakenTokenContractAddress = LpTokentContractAddress
            });
            await AwakenSwapContractStub.SetFeeRate.SendAsync(new Int64Value(){Value = 30});
            await UserTomSwapStub.CreatePair.SendAsync(new CreatePairInput()
            {
                SymbolPair = "ELF-TEST"
            });
        }
    }
}