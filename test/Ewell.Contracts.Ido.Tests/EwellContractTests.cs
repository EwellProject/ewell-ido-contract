using System;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Ewell.Contracts.Whitelist;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Ewell.Contracts.Ido
{
    public class EwellContractTests : EwellContractTestBase
    {
        private Hash projectId0;
        private const long TotalSupply = 100000000_00000000;
        private readonly int _chainId = ChainHelper.ConvertBase58ToChainId("AELF");

        private const string TestSymbol = "TEST-1";
        private const string WhitelistUrl = "WhitelistUrl";

        [Fact]
        public async Task InitializeTest()
        {
            await CreateAndGetTokenNew();
            await AdminStub.Initialize.SendAsync(new InitializeInput()
            {
                WhitelistContractAddress = WhitelistContractAddress,
                ProxyAccountContractAddress = ProxyAccountContractAddress
            });
            var whitelistAddress = await AdminStub.GetWhitelistContractAddress.CallAsync(new Empty());
            whitelistAddress.ShouldNotBe(new Address());
            
            var proxyAccountContract = await AdminStub.GetProxyAccountContract.CallAsync(new Empty());
            proxyAccountContract.ShouldNotBe(new Address());
        }
        
        [Fact]
        public async Task SetProxyAccountContract_Test()
        {
            await InitializeTest();
            var result = await TomStub.SetProxyAccountContract.SendWithExceptionAsync(ProxyAccountContractAddress);
            result.TransactionResult.Error.ShouldContain("No permission.");
            result = await AdminStub.SetProxyAccountContract.SendWithExceptionAsync(new Address());
            result.TransactionResult.Error.ShouldContain("Invalid param");
            result = await AdminStub.SetProxyAccountContract.SendAsync(ProxyAccountContractAddress);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var proxyAccountContract = await AdminStub.GetProxyAccountContract.CallAsync(new Empty());
            proxyAccountContract.ShouldBe(ProxyAccountContractAddress);
        }

        [Fact]
        public async Task RegisterTest()
        {
            await InitializeTest();
            
            var virtualAddress = await AdminStub.GetPendingProjectAddress.CallAsync(AdminAddress);
            var senderBalanceBefore = await GetBalanceAsync(TestSymbol, AdminAddress);
            var virtualAddressBalanceBefore = await GetBalanceAsync(TestSymbol, virtualAddress);
            
            //approve first
            await TokenContractStub.Approve.SendAsync(new ApproveInput()
           {
               Amount = 1000_00000000,
               Symbol = TestSymbol,
               Spender = EwellContractAddress
           });
            
            var registerInput = new RegisterInput()
            {
                AcceptedSymbol = "ELF",
                ProjectSymbol =  TestSymbol,
                CrowdFundingType = "price sale",
                CrowdFundingIssueAmount = 1000_00000000,
                PreSalePrice = 50000000,
                StartTime = blockTimeProvider.GetBlockTime().AddSeconds(3),
                EndTime = blockTimeProvider.GetBlockTime().AddSeconds(30),
                MinSubscription = 10,
                MaxSubscription = 100,
                IsEnableWhitelist = true,
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
                TokenReleaseTime = blockTimeProvider.GetBlockTime().AddSeconds(30),
                WhitelistUrl = WhitelistUrl
            };
            var executionResult = await AdminStub.Register.SendAsync(registerInput);

            
            //check balance
            var senderBalanceAfter = await GetBalanceAsync(TestSymbol, AdminAddress);
            var virtualAddressBalanceAfter = await GetBalanceAsync(TestSymbol, virtualAddress);

            senderBalanceAfter.ShouldBe(999000_00000000);
            virtualAddressBalanceAfter.ShouldBe(1000_00000000);
            
            var projectId = ProjectRegistered.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(ProjectRegistered)))
                    .NonIndexed)
                .ProjectId;
            var whitelistUrlLog = WhitelistCreated.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(WhitelistCreated)))
                    .NonIndexed)
                .Url;
            var whitelistIdLog = WhitelistCreated.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(WhitelistCreated)))
                    .NonIndexed)
                .WhitelistId;

            var whitelistId = await AdminStub.GetWhitelistId.CallAsync(projectId);
            whitelistId.ShouldBe(whitelistIdLog);
            WhitelistUrl.ShouldBe(whitelistUrlLog);
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
            
            await AdminStub.AddWhitelists.SendAsync(new AddWhitelistsInput()
            {
                ProjectId = projectId0,
                Users = { UserTomAddress }
            });
        }
        
        [Fact]
        public async Task AddWhitelistsTest()
        {
            await RegisterTest();

            await AdminStub.AddWhitelists.SendAsync(new AddWhitelistsInput()
            {
                ProjectId = projectId0,
                Users = { UserTomAddress }
            });
        }

        [Fact]
        public async Task InvestTest()
        {
            var investAmount = 100;
            await AddWhitelistsTest();
            
            blockTimeProvider.SetBlockTime(blockTimeProvider.GetBlockTime().AddSeconds(3));
            //var balance1 = await GetBalanceAsync("ELF", UserTomAddress);
            await TomTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = EwellContractAddress,
                Amount = 1000,
                Symbol = "ELF"
            });
            //var balance1 = await GetBalanceAsync("ELF", UserTomAddress);
            await TomStub.Invest.SendAsync(new InvestInput()
            {
                ProjectId = projectId0,
                Symbol = "ELF",
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
        public async Task UpdateAdditionalInfo_Test()
        {
            await AddWhitelistsTest();
            
            await AdminStub.UpdateAdditionalInfo.SendAsync(new UpdateAdditionalInfoInput()
            {
                ProjectId = projectId0,
                AdditionalInfo = new AdditionalInfo
                {
                    Data =
                    {
                        ["test1"] = "Additional test1",
                        ["test2"] = "Additional test2",
                    }
                } 
            });
            
            //check
            var projectInfoAfter = await AdminStub.GetProjectInfo.CallAsync(projectId0);
            projectInfoAfter.ShouldNotBeNull();
            projectInfoAfter.AdditionalInfo.Data.ShouldNotBeNull();
            projectInfoAfter.AdditionalInfo.Data.ShouldContainKey("test1");
        }
        
        [Fact]
        public async Task GetTokenContractAddress_Test()
        {
            await InitializeTest();
            
            //check
            var tokenContractAddress = await AdminStub.GetTokenContractAddress.CallAsync(new Empty());
            tokenContractAddress.ShouldNotBeNull();
            tokenContractAddress.ShouldBe(TokenContractAddress);
        }
        
        [Fact]
        public async Task GetProjectListInfo_Test()
        {
            await RegisterTest();
            
            //check
            var projectListInfo = await TomStub.GetProjectListInfo.CallAsync(projectId0);
            projectListInfo.ShouldNotBeNull();
        }
        
        [Fact]
        public async Task GetAdmin_Test()
        {
            await InitializeTest();
            
            //check
            var adminAddress = await AdminStub.GetAdmin.CallAsync(new Empty());
            adminAddress.ShouldNotBeNull();
            adminAddress.ShouldBe(AdminAddress);
        }
        
        [Fact]
        public async Task GetPendingProjectAddressTest()
        {
            await InitializeTest();
            var virtualAddressExpect = await AdminStub.GetPendingProjectAddress.CallAsync(AdminAddress);
            
            await TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Amount = 1_00000000,
                Symbol = TestSymbol,
                Spender = EwellContractAddress
            });

            var registerInput = new RegisterInput()
            {
                AcceptedSymbol = "ELF",
                ProjectSymbol = TestSymbol,
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
            var whitelistIdLog = WhitelistCreated.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name.Contains(nameof(WhitelistCreated)))
                    .NonIndexed)
                .WhitelistId;

            var whitelistId = await AdminStub.GetWhitelistId.CallAsync(projectId);
            whitelistId.ShouldBe(whitelistIdLog);
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