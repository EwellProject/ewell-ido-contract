using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.Ewell.Tests;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AElf.Contracts.Ewell
{
    public class EwellContractTests : EwellContractTestBase
    {
        private Hash projectId0;
        private const long TotalSupply = 100000000_00000000;
        private readonly int _chainId = ChainHelper.ConvertBase58ToChainId("AELF");

        private const string TestSymbol = "TEST-1";

        [Fact]
        public async Task InitializeTest()
        {
            await CreateAndGetTokenNew();
            await AdminStub.Initialize.SendAsync(new InitializeInput()
            {
                WhitelistContract = WhitelistContractAddress
            });
            var whitelistAddress = await AdminStub.GetWhitelistContractAddress.CallAsync(new Empty());
            whitelistAddress.ShouldNotBe(new Address());
            var virtualAddress = await AdminStub.GetPendingProjectAddress.CallAsync(AdminAddress);
            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                Amount = 1_00000000,
                Symbol = TestSymbol,
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
                ProjectCurrency =  TestSymbol,
                CrowdFundingType = "price sale",
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
                    Data =
                    {
                        new ListMarket()
                        {
                            Market = AwakenSwapContractAddress,
                            Weight = 100
                        }
                    }
                },
                UnlockTime = Timestamp.FromDateTime(DateTime.UtcNow.Add(new TimeSpan(0, 0, 30000))),
                TotalPeriod = 1,
                FirstDistributeProportion = 100_000000,
                RestDistributeProportion = 0,
                PeriodDuration = 0,
                TokenReleaseTime = blockTimeProvider.GetBlockTime().AddSeconds(30)
            };
            var executionResult = await AdminStub.Register.SendAsync(registerInput);

            var projectId = ProjectRegistered.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(ProjectRegistered)))
                    .NonIndexed)
                .ProjectId;

            var whitelistId = await AdminStub.GetWhitelistId.CallAsync(projectId);
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
                Users = { UserTomAddress }
            });

            await AdminStub.RemoveWhitelists.SendAsync(new RemoveWhitelistsInput()
            {
                ProjectId = projectId0,
                Users = { UserTomAddress }
            });
        }

        [Fact]
        public async Task InvestTest()
        {
            var investAmount = 100;
            await RegisterTest();
            
            blockTimeProvider.SetBlockTime(blockTimeProvider.GetBlockTime().AddSeconds(3));
            //var balance1 = await GetBalanceAsync("ELF", UserTomAddress);
            await TomTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = EwellContractAddress,
                Amount = 10000,
                Symbol = "ELF"
            });
            //var balance1 = await GetBalanceAsync("ELF", UserTomAddress);
            await TomStub.Invest.SendAsync(new InvestInput()
            {
                ProjectId = projectId0,
                Currency = "ELF",
                InvestAmount = investAmount
            });
            
            //after user invest and check 
            var investDetail = await TomStub.GetInvestDetail.CallAsync(new GetInvestDetailInput()
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
            
            //check repeat Claim
            await TomStub.Claim.SendAsync(new ClaimInput()
            {
                ProjectId = projectId0,
                User = UserTomAddress
            });
            
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = TestSymbol
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
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = virtualAddress,
                Symbol = TestSymbol
            });

            await AdminStub.Cancel.SendAsync(projectId0);

            var projectInfoAfter = await AdminStub.GetProjectInfo.CallAsync(projectId0);
            projectInfoAfter.Enabled.ShouldBeFalse();
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = virtualAddress,
                Symbol = TestSymbol
            });
            balanceBefore.Balance.ShouldBePositive();
            balanceAfter.Balance.ShouldBe(0);
        }

        [Fact]
        public async Task ClaimLiquidatedDamageTest()
        {
            await UnInvestTest();
            await AdminStub.Cancel.SendAsync(projectId0);

            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            await TomStub.ClaimLiquidatedDamage.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
            var liquidatedDamageDetails = await TomStub.GetLiquidatedDamageDetails.CallAsync(projectId0);
            var liquidatedDamage = liquidatedDamageDetails.Details.First(x => x.User == UserTomAddress);
            liquidatedDamage.Claimed.ShouldBe(true);
        }

        [Fact]
        public async Task UnInvestTest()
        {
            await InvestTest();
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });

            await TomStub.UnInvest.SendAsync(projectId0);

            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
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
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });

            await TomStub.ReFund.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
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
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });

            await AdminStub.ReFundAll.SendAsync(new ReFundAllInput()
            {
                ProjectId = projectId0,
                Users = { UserTomAddress }
            });
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
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

            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            await AdminStub.ClaimLiquidatedDamageAll.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = UserTomAddress,
                Symbol = "ELF"
            });
            balanceAfter.Balance.Sub(balanceBefore.Balance).ShouldBePositive();
            var liquidatedDamageDetails = await TomStub.GetLiquidatedDamageDetails.CallAsync(projectId0);
            var liquidatedDamage = liquidatedDamageDetails.Details.First(x => x.User == UserTomAddress);
            liquidatedDamage.Claimed.ShouldBe(true);
        }


        [Fact]
        public async Task WithdrawTest()
        {
            await InvestTest();

            blockTimeProvider.SetBlockTime(blockTimeProvider.GetBlockTime().AddSeconds(30));
            var balanceBefore = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = AdminAddress,
                Symbol = "ELF"
            });
            await AdminStub.Withdraw.SendAsync(projectId0);
            var balanceAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
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
                ProjectCurrency = TestSymbol,
                CrowdFundingType = "price sale",
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
                    Data =
                    {
                        new ListMarket()
                        {
                            Market = AwakenSwapContractAddress,
                            Weight = 100
                        }
                    }
                },
                UnlockTime = Timestamp.FromDateTime(DateTime.UtcNow.Add(new TimeSpan(0, 0, 30000))),
                TotalPeriod = 1,
                FirstDistributeProportion = 100_000000,
                RestDistributeProportion = 0,
                PeriodDuration = 0,
                TokenReleaseTime = blockTimeProvider.GetBlockTime().AddSeconds(30)
            };

            var executionResult = await AdminStub.Register.SendAsync(registerInput);

            var projectId = ProjectRegistered.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(ProjectRegistered)))
                    .NonIndexed)
                .ProjectId;

            var whitelistId = await AdminStub.GetWhitelistId.CallAsync(projectId);
            whitelistId.ShouldBe(HashHelper.ComputeFrom(0));
            projectId0 = projectId;

            var virtualAddress = await AdminStub.GetProjectAddressByProjectHash.CallAsync(projectId0);
            virtualAddress.ShouldBe(virtualAddressExpect);
        }
        
        [Fact]
        public async Task CreateNftCollectionAndNft()
        {
            var collectionInfo = NftCollectionInfo("TEST", "TEST Collection");
            var createCollectionRes = await CreateNftCollectionAsync(collectionInfo);
            createCollectionRes.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var testNftInfo = NftInfo("1", "TEST 1 symbol");
            var createNftRes = await CreateNftAsync(collectionInfo.Symbol, testNftInfo);
            createNftRes.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        
        [Fact]
        public async Task CreateAndGetTokenNew()
        {
            var collectionInfo = NftCollectionInfo("TEST", "TEST Collection");
            var createCollectionRes = await CreateNftCollectionAsync(collectionInfo);
            createCollectionRes.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var testNftInfo = NftInfo("1", "TEST 1 symbol");
            var createNftRes = await CreateNftAsync(collectionInfo.Symbol, testNftInfo);
            createNftRes.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var issueResult = await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = 100000000000000,
                Symbol = TestSymbol,
                To = AdminAddress
            });
            issueResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
            {
                Owner = AdminAddress,
                Symbol = TestSymbol
            });
            balance.Balance.ShouldBe(100000000000000);
            
            //Recharge
            await TokenContractStub.Transfer.SendAsync(new AElf.Contracts.MultiToken.TransferInput()
            {
                Amount = 100000000000,
                Symbol = "ELF",
                Memo = "Recharge",
                To = UserTomAddress
            });
        }

        private async Task<IExecutionResult<Empty>> CreateNftAsync(string colllectionSymbol, TokenInfo nftInfo)
        {
            var input = new CreateInput
            {
                Symbol = $"{colllectionSymbol}{nftInfo.Symbol}",
                TokenName = nftInfo.TokenName,
                TotalSupply = nftInfo.TotalSupply,
                Decimals = nftInfo.Decimals,
                Issuer = nftInfo.Issuer,
                IsBurnable = nftInfo.IsBurnable,
                IssueChainId = nftInfo.IssueChainId,
                ExternalInfo = nftInfo.ExternalInfo,
                Owner = nftInfo.Issuer
            };
            return await TokenContractStub.Create.SendAsync(input);
        }
        
        private async Task<IExecutionResult<Empty>> CreateNftCollectionAsync(TokenInfo collectionInfo)
        {
            return await CreateMutiTokenAsync(TokenContractStub, new CreateInput
            {
                Symbol = $"{collectionInfo.Symbol}0",
                TokenName = collectionInfo.TokenName,
                TotalSupply = collectionInfo.TotalSupply,
                Decimals = collectionInfo.Decimals,
                Issuer = collectionInfo.Issuer,
                Owner = collectionInfo.Issuer,
                IssueChainId = collectionInfo.IssueChainId,
                ExternalInfo = collectionInfo.ExternalInfo
            });
        }
        
        private TokenInfo NftCollectionInfo(string symbol, string tokenName) => new()
        {
            Symbol = $"{symbol}-",
            Decimals = 0,
            TotalSupply = 1,
            TokenName = tokenName,
            Issuer = AdminAddress,
            Owner = AdminAddress,
            IssueChainId = _chainId
        };
        
        private TokenInfo NftInfo(string symbol, string tokenName) => new()
        {
            Symbol = symbol,
            Decimals = 0,
            TotalSupply = TotalSupply,
            TokenName = tokenName,
            Issuer = AdminAddress,
            Owner = AdminAddress,
            IsBurnable = true,
            IssueChainId = _chainId
        };
    }
}