using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Whitelist;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Ewell
{
    public partial class EwellContract : EwellContractContainer.EwellContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.TokenContract.Value == null, "Already initialized.");
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.WhitelistContract.Value = input.WhitelistContract;
            State.Admin.Value = Context.Sender;
            return new Empty();
        }

        public override Empty Register(RegisterInput input)
        {
            ValidTokenSymbolOwner(input.ProjectCurrency, Context.Sender);
            ValidTokenSymbol(input.AcceptedCurrency);
            Assert(input.MaxSubscription > input.MinSubscription && input.MinSubscription > 0,"Invalid subscription input");
            Assert(input.StartTime <= input.EndTime && input.StartTime > Context.CurrentBlockTime,"Invalid startTime or endTime input");
            Assert(input.FirstDistributeProportion.Add(input.TotalPeriod.Sub(1).Mul(input.RestDistributeProportion)) <= ProportionMax,"Invalid distributeProportion input");
            var toRaisedAmount = Parse(new BigIntValue(input.CrowdFundingIssueAmount).Mul(Mantissa).Div(input.PreSalePrice).Value);
            Assert(toRaisedAmount > 0, "Invalid raise amount calculated from input");
            var id = GetHash(input, Context.Sender);
            var virtualAddressHash = GetProjectVirtualAddressHash(Context.Sender); 
            var virtualAddress = Context.ConvertVirtualAddressToContractAddress(virtualAddressHash);
            ValidTokenBalance(input.ProjectCurrency, virtualAddress, input.CrowdFundingIssueAmount);
            State.ProjectAddressMap[id] = virtualAddress;
            State.ProjectCreatorIndexMap[Context.Sender] = State.ProjectCreatorIndexMap[Context.Sender].Add(1);
            var projectInfo = new ProjectInfo()
            {
                ProjectId = id,
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
                Creator = Context.Sender,
                ToRaisedAmount = toRaisedAmount,
                Enabled = true,
                VirtualAddressHash = virtualAddressHash
            };
            State.ProjectInfoMap[id] = projectInfo;
            var listInfo = new ProjectListInfo()
            {
                ProjectId = id,
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
            State.ProjectListInfoMap[id] = listInfo;
            
            //SubscribeWhiteList
            if (input.WhitelistId != null)
            {
                State.WhiteListIdMap[id] = input.WhitelistId;
            }
            else
            {
                //create whitelist
                State.WhitelistContract.CreateWhitelist.Send(new CreateWhitelistInput()
                {
                    Creator = Context.Self,
                    ProjectId = id,
                    ExtraInfoList = new ExtraInfoList(),
                    ManagerList = new AddressList(){Value = { Context.Self, Context.Sender}}
                });
                //write id to state and set enabled state
                Context.SendInline(Context.Self, nameof(SetWhitelistId), new SetWhitelistIdInput()
                {
                    ProjectId = id,
                    IsEnableWhitelist = input.IsEnableWhitelist
                });
            }
            
            Context.Fire(new ProjectRegistered()
            {
                ProjectId = id,
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
                Creator = Context.Sender,
                FirstDistributeProportion = input.FirstDistributeProportion,
                RestDistributeProportion = input.RestDistributeProportion,
                PeriodDuration = input.PeriodDuration
            });
            return new Empty();
        }

        public override Empty AddWhitelists(AddWhitelistsInput input)
        {
            var projectInfo = ValidProjectExist(input.ProjectId);
            Assert(projectInfo.Enabled,"Project is not enabled");
            ValidProjectOwner(input.ProjectId);

            var whitelistId = State.WhiteListIdMap[input.ProjectId];
            var whitelistInfo = State.WhitelistContract.GetWhitelist.Call(whitelistId);
            Assert(whitelistInfo.IsAvailable,"Whitelist is not enabled");
            
            var list = new AddressList();
            foreach (var user in input.Users)
            {
                list.Value.Add(user);
            }
          
            State.WhitelistContract.AddAddressInfoListToWhitelist.Send(new AddAddressInfoListToWhitelistInput()
            {
                ExtraInfoIdList = new Whitelist.ExtraInfoIdList(){Value = { new Whitelist.ExtraInfoId()
                {
                    AddressList = list
                }}},
                WhitelistId = whitelistId
            });
            return new Empty();
        }

        public override Empty RemoveWhitelists(RemoveWhitelistsInput input)
        {
            var projectInfo = ValidProjectExist(input.ProjectId);
            Assert(projectInfo.Enabled,"Project is not enabled");
            ValidProjectOwner(input.ProjectId);

            var whitelistId = State.WhiteListIdMap[input.ProjectId];
            var whitelistInfo = State.WhitelistContract.GetWhitelist.Call(whitelistId);
            Assert(whitelistInfo.IsAvailable,"Whitelist is not enabled");
            
            var list = new AddressList();
            foreach (var user in input.Users)
            {
                list.Value.Add(user);
            }
            State.WhitelistContract.RemoveAddressInfoListFromWhitelist.Send(new RemoveAddressInfoListFromWhitelistInput()
            {
                ExtraInfoIdList = new Whitelist.ExtraInfoIdList(){Value = { new Whitelist.ExtraInfoId()
                {
                    AddressList = list
                }}},
                WhitelistId = whitelistId
            });
            return new Empty();
        }
        
        public override Empty UpdateAdditionalInfo(UpdateAdditionalInfoInput input)
        {
            var projectInfo = ValidProjectExist(input.ProjectId);
            Assert(projectInfo.Enabled,"Project is not enabled");
            ValidProjectOwner(input.ProjectId);
            State.ProjectInfoMap[input.ProjectId].AdditionalInfo = input.AdditionalInfo;
            Context.Fire(new AdditionalInfoUpdated()
            {
                ProjectId = input.ProjectId,
                AdditionalInfo = input.AdditionalInfo
            });
            return new Empty();
        }

        public override Empty Cancel(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(projectInfo.Enabled,"Project is not enabled");
            ValidProjectOwner(input);
            Assert(Context.CurrentBlockTime <= projectInfo.EndTime,"time is expired");
            State.ProjectInfoMap[input].Enabled = false;
            Context.Fire(new ProjectCanceled()
            {
                ProjectId = input
            });
            
            //burn or back to creator
            var toBurnAmount = projectInfo.CrowdFundingIssueAmount;
            if (projectInfo.IsBurnRestToken)
            {
                State.TokenContract.Burn.VirtualSend(projectInfo.VirtualAddressHash, new BurnInput()
                {
                    Symbol = projectInfo.ProjectCurrency,
                    Amount = toBurnAmount
                });
            }
            else
            {
                TransferOut(input, projectInfo.Creator, projectInfo.ProjectCurrency, toBurnAmount);
            }

            return new Empty();
        }

        public override Empty NextPeriod(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(projectInfo.Enabled,"Project is not enabled");
            AdminCheck();
            var projectListInfo = State.ProjectListInfoMap[input];
            Assert(projectListInfo.LatestPeriod < projectListInfo.TotalPeriod,"Insufficient period");
            var nextPeriodTime =
                projectInfo.EndTime.Seconds.Add(projectListInfo.PeriodDuration.Mul(projectListInfo.LatestPeriod));
            Assert(Context.CurrentBlockTime.Seconds >= nextPeriodTime,"Time is not ready");
            var newPeriod = State.ProjectListInfoMap[input].LatestPeriod.Add(1);
            State.ProjectListInfoMap[input].LatestPeriod = newPeriod;
            Context.Fire(new PeriodUpdated()
            {
                ProjectId = input,
                NewPeriod = newPeriod
            });
            return new Empty();
        }

        
        public override Empty Invest(InvestInput input)
        {
            //check status
            var projectInfo = ValidProjectExist(input.ProjectId);
            Assert(projectInfo.Enabled,"Project is not enabled");
            var whitelistId = State.WhiteListIdMap[input.ProjectId];
            var whitelistInfo = State.WhitelistContract.GetWhitelist.Call(whitelistId);
            if (whitelistInfo.IsAvailable)
            {
                WhitelistCheck(input.ProjectId, Context.Sender);
            }
            Assert(projectInfo.AcceptedCurrency == input.Currency,"The currency is invalid");
            CheckInvestInput(input.ProjectId, Context.Sender, input.InvestAmount);
            var currentTimestamp = Context.CurrentBlockTime;
            Assert(currentTimestamp >= projectInfo.StartTime && currentTimestamp <= projectInfo.EndTime,"Can't invest right now");
            //invest 
          
            TransferIn(input.ProjectId, Context.Sender, input.Currency, input.InvestAmount);
            var investDetail =  State.InvestDetailMap[projectInfo.ProjectId][Context.Sender] ?? new InvestDetail()
            {
                InvestSymbol = input.Currency,
                Amount = 0
            };
             
            var totalInvestAmount = investDetail.Amount.Add(input.InvestAmount);
            investDetail.Amount = totalInvestAmount;
            investDetail.IsUnInvest = false;
            State.InvestDetailMap[projectInfo.ProjectId][Context.Sender] = investDetail;
            State.ProjectInfoMap[input.ProjectId].CurrentRaisedAmount = State.ProjectInfoMap[input.ProjectId]
                .CurrentRaisedAmount.Add(input.InvestAmount);
            
            Assert(State.ProjectInfoMap[input.ProjectId].CurrentRaisedAmount <= projectInfo.ToRaisedAmount,"The investment quota is already full");
            var toClaimAmount = ProfitDetailUpdate(input.ProjectId, Context.Sender, totalInvestAmount);
            
            Context.Fire(new Invested()
            {
                ProjectId = input.ProjectId,
                InvestSymbol = input.Currency,
                Amount = input.InvestAmount,
                TotalAmount = totalInvestAmount,
                ProjectCurrency = projectInfo.ProjectCurrency,
                ToClaimAmount = toClaimAmount,
                User = Context.Sender
            });
            return new Empty();
        }

        public override Empty UnInvest(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(projectInfo.Enabled,"Project is not enabled");
            var currentTimestamp = Context.CurrentBlockTime;
            Assert(currentTimestamp >= projectInfo.StartTime && currentTimestamp <= projectInfo.EndTime,"Can't unInvest right now");
            //unInvest 
            var userinfo = State.InvestDetailMap[input][Context.Sender];
            Assert(userinfo != null,"No invest record");
            Assert(!userinfo.IsUnInvest,"User has already unInvest");
            var userAmount = userinfo.Amount;
            Assert(userAmount > 0,"Insufficient invest amount");
            State.LiquidatedDamageDetailsMap[input] =
                State.LiquidatedDamageDetailsMap[input] ?? new LiquidatedDamageDetails();
            var liquidatedDamageDetails = State.LiquidatedDamageDetailsMap[input];
            var liquidatedDamageAmountStr = new BigIntValue(userinfo.Amount).Mul(LiquidatedDamageProportion).Div(ProportionMax);
            var liquidatedDamageAmount = Parse(liquidatedDamageAmountStr.Value);
            var detail = new LiquidatedDamageDetail()
            {
                Amount = liquidatedDamageAmount,
                Symbol = userinfo.InvestSymbol,
                User = Context.Sender
            };

            State.InvestDetailMap[input][Context.Sender].IsUnInvest = true;
            var unInvestAmount = userAmount.Sub(liquidatedDamageAmount);
            if (unInvestAmount > 0)
            {
                TransferOut(input, Context.Sender,userinfo.InvestSymbol, unInvestAmount);
            }
            
            State.InvestDetailMap[input][Context.Sender].Amount = 0;
            State.ProjectInfoMap[input].CurrentRaisedAmount = State.ProjectInfoMap[input]
                .CurrentRaisedAmount.Sub(userAmount);
            
            ProfitDetailUpdate(input, Context.Sender, 0);
            
            Context.Fire(new UnInvested()
            {
                ProjectId = input,
                User = Context.Sender,
                InvestSymbol = userinfo.InvestSymbol,
                TotalAmount = userAmount,
                UnInvestAmount = unInvestAmount
            });
            
            liquidatedDamageDetails.Details.Add(detail);
            liquidatedDamageDetails.TotalAmount = liquidatedDamageDetails.TotalAmount.Add(liquidatedDamageAmount);
            State.LiquidatedDamageDetailsMap[input] = liquidatedDamageDetails;
            Context.Fire(new LiquidatedDamageRecord()
            {
                ProjectId = input,
                User = Context.Sender,
                InvestSymbol = userinfo.InvestSymbol,
                Amount = liquidatedDamageAmount
            });
            return new Empty();
        }

        // public override Empty LockLiquidity(Hash input)
        // {
            // var projectInfo = ValidProjectExist(input);
            // Assert(projectInfo.Enabled,"project is not enabled");
            // // ValidProjectOwner(input);
            // var projectListInfo = State.ProjectListInfoMap[input];
            // Assert(!projectListInfo.IsListed, "already listed");
            // Assert(Context.CurrentBlockTime >= projectListInfo.UnlockTime,"time is not ready");
            // var acceptedCurrencyAmount = projectInfo.CurrentRaisedAmount.Mul(projectListInfo.LiquidityLockProportion)
            //     .Div(ProportionMax);
            //
            // var projectCurrencyAmount = acceptedCurrencyAmount.Mul(projectListInfo.PublicSalePrice).Div(Mantissa);
            //
            // foreach (var market in projectListInfo.ListMarketInfo.Data )
            // {
            //     var amountADesired = acceptedCurrencyAmount.Mul(market.Weight).Div(ProportionMax);
            //     var amountBDesired = projectCurrencyAmount.Mul(market.Weight).Div(ProportionMax);
            //     
            //     State.TokenContract.Approve.Send(new ApproveInput()
            //     {
            //         Spender = market.Market,
            //         Symbol = projectInfo.AcceptedCurrency,
            //         Amount = amountADesired
            //     });
            //     State.TokenContract.Approve.Send(new ApproveInput()
            //     {
            //         Spender = market.Market,
            //         Symbol = projectInfo.ProjectCurrency,
            //         Amount = amountBDesired
            //     });
            //     State.SwapContract.Value = market.Market;
            //     State.SwapContract.AddLiquidity.Send(new AddLiquidityInput()
            //     {
            //         SymbolA = projectInfo.AcceptedCurrency,
            //         SymbolB = projectInfo.ProjectCurrency,
            //         AmountADesired = amountADesired,
            //         AmountBDesired = amountBDesired,
            //         AmountAMin = 0,
            //         AmountBMin = 0,
            //         Channel = "",
            //         Deadline = Context.CurrentBlockTime.AddMinutes(3),
            //         To = Context.Sender
            //     });
            //     State.ProjectListInfoMap[input].IsListed = true;
            // }
            //      return new Empty();
            // }

        public override Empty Withdraw(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(projectInfo.Enabled,"Project is not enabled");
            AdminCheck();
            var projectListInfo = State.ProjectListInfoMap[input];
            Assert(!projectListInfo.IsWithdraw,"Already withdraw" );
            Assert(Context.CurrentBlockTime >= projectInfo.EndTime,"Time is not ready");
            var withdrawAmount = projectInfo.CurrentRaisedAmount;
            if (withdrawAmount > 0)
            {
                TransferOut(input, projectInfo.Creator, projectInfo.AcceptedCurrency, withdrawAmount);
            }
           
            State.ProjectListInfoMap[input].IsWithdraw = true;
            var liquidatedDamageDetails = State.LiquidatedDamageDetailsMap[input];
            if (liquidatedDamageDetails != null && liquidatedDamageDetails.TotalAmount > 0)
            {
                TransferOut(input, projectInfo.Creator, projectInfo.AcceptedCurrency, liquidatedDamageDetails.TotalAmount);
            }
            var profitStr =  new BigIntValue(projectInfo.CurrentRaisedAmount).Mul(projectInfo.PreSalePrice).Div(Mantissa).Value;
            var profit = Parse(profitStr);
            var toBurnAmount = projectInfo.CrowdFundingIssueAmount.Sub(profit);
            if (projectInfo.IsBurnRestToken)
            {
              
                State.TokenContract.Burn.VirtualSend(projectInfo.VirtualAddressHash, new BurnInput()
                {
                    Symbol = projectInfo.ProjectCurrency,
                    Amount = toBurnAmount
                });
            }
            else
            {
                TransferOut(input, projectInfo.Creator, projectInfo.ProjectCurrency, toBurnAmount);
            }
            
            Context.Fire(new Withdrawn()
            {
                ProjectId = input,
                AcceptedSymbol = projectInfo.AcceptedCurrency,
                WithdrawAmount = withdrawAmount,
                ProjectCurrency = projectInfo.ProjectCurrency,
                IsBurnRestToken = projectInfo.IsBurnRestToken,
                BurnAmount = toBurnAmount
            });
            return new Empty();
        }

      

        public override Empty Claim(ClaimInput input)
        {
            //check status
            var projectInfo = ValidProjectExist(input.ProjectId);
            Assert(projectInfo.Enabled,"Project is not enabled");

            var listInfo = State.ProjectListInfoMap[input.ProjectId];
            var profitDetailInfo = State.ProfitDetailMap[input.ProjectId][input.User];
            Assert(profitDetailInfo.AmountsMap.Count > 0, "No invest record");
            State.ClaimedProfitsInfoMap[input.User] = State.ClaimedProfitsInfoMap[input.User]?? new ClaimedProfitsInfo();
            var claimedProfitsInfo = State.ClaimedProfitsInfoMap[input.User];
            for (var i = profitDetailInfo.LatestPeriod + 1; i <= listInfo.LatestPeriod; i++)
            {
                var currentPeriod = i ;
                var profitPeriodAmount = profitDetailInfo.AmountsMap[currentPeriod];
                if (profitPeriodAmount > 0)
                {
                    TransferOut(input.ProjectId, input.User, profitDetailInfo.Symbol, profitPeriodAmount);
                }
                claimedProfitsInfo.Details.Add(new ClaimedProfit()
                {
                    ProjectId = input.ProjectId,
                    LatestPeriod = currentPeriod,
                    Symbol = profitDetailInfo.Symbol,
                    Amount = profitPeriodAmount
                });
            
                State.ClaimedProfitsInfoMap[input.User] = claimedProfitsInfo;
            
                Context.Fire(new Claimed()
                {
                    ProjectId = input.ProjectId,
                    LatestPeriod = currentPeriod,
                    Amount = profitPeriodAmount,
                    ProjectCurrency = profitDetailInfo.Symbol,
                    TotalPeriod = listInfo.LatestPeriod,
                    User = input.User
                });
            }
            State.ProfitDetailMap[input.ProjectId][input.User].LatestPeriod = listInfo.LatestPeriod;
            
            return new Empty();
        }

        public override Empty SetWhitelistId(SetWhitelistIdInput input)
        {
            Assert(Context.Sender == Context.Self,"Only self contract can call this function");
            var whitelistIdList = State.WhitelistContract.GetWhitelistByProject.Call(input.ProjectId);
            var whitelistId = whitelistIdList.WhitelistId.First();
            State.WhiteListIdMap[input.ProjectId] = whitelistId;
            if (!input.IsEnableWhitelist)
            {
                State.WhitelistContract.DisableWhitelist.Send(whitelistId);
            }
            Context.Fire(new NewWhitelistIdSet()
            {
                ProjectId = input.ProjectId,
                WhitelistId = whitelistId
            });
            return new Empty();
        }

        public override Empty ReFund(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(!projectInfo.Enabled ,"Project should be disabled");
            //unInvest 
            ReFundInternal(input, Context.Sender);
            return new Empty();
        }

        public override Empty ReFundAll(ReFundAllInput input)
        {
            var projectInfo = ValidProjectExist(input.ProjectId);
            Assert(!projectInfo.Enabled ,"Project should be disabled");
            AdminCheck();
            foreach (var user in input.Users)
            {
                ReFundInternal(input.ProjectId, user);
            }
            
            return new Empty();
        }

        public override Empty ClaimLiquidatedDamage(Hash input)
        {
            //check status
            var projectInfo = ValidProjectExist(input);
            Assert(!projectInfo.Enabled,"Project should be disabled");

            var details = State.LiquidatedDamageDetailsMap[input].Details.Where(x => x.User == Context.Sender);
            foreach (var detail in details)
            {
                Assert(!detail.Claimed,"Already claimed");
                if (detail.Amount > 0)
                {
                    TransferOut(input, detail.User, detail.Symbol, detail.Amount);
                }
                
                detail.Claimed = true;
                Context.Fire(new LiquidatedDamageClaimed()
                {
                    ProjectId = input,
                    Amount = detail.Amount,
                    InvestSymbol = detail.Symbol,
                    User = detail.User
                });
            }
         
            return new Empty();
        }
        public override Empty ClaimLiquidatedDamageAll(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(!projectInfo.Enabled,"Project should be disabled");
            AdminCheck();
            var details = State.LiquidatedDamageDetailsMap[input].Details;
            foreach (var detail in details)
            {
                Assert(!detail.Claimed,"Already claimed");
                if (detail.Amount > 0)
                {
                    TransferOut(input, detail.User, detail.Symbol, detail.Amount);
                }
                
                detail.Claimed = true;
                Context.Fire(new LiquidatedDamageClaimed()
                {
                    ProjectId = input,
                    Amount = detail.Amount,
                    InvestSymbol = detail.Symbol,
                    User = detail.User
                });
            }

            return new Empty();
        }
    }
}