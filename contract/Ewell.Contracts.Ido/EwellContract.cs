using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Ewell.Contracts.Whitelist;
using Google.Protobuf.WellKnownTypes;

namespace Ewell.Contracts.Ido
{
    public partial class EwellContract : EwellContractContainer.EwellContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");
            State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
            Assert(State.GenesisContract.GetContractAuthor.Call(Context.Self) == Context.Sender, "No permission.");
            Assert(input.WhitelistContractAddress != null && !input.WhitelistContractAddress.Value.IsNullOrEmpty(),
                "WhitelistContractAddress required.");
            Assert(input.ProxyAccountContractAddress != null && !input.ProxyAccountContractAddress.Value.IsNullOrEmpty(),
                "ProxyAccountContractAddress required.");
            State.WhitelistContract.Value = input.WhitelistContractAddress;
            State.ProxyAccountContract.Value = input.ProxyAccountContractAddress;
            State.Admin.Value = input.AdministratorAddress ?? Context.Sender;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.Initialized.Value = true;
            return new Empty();
        }
        
        public override Empty SetProxyAccountContract(Address input)
        {
            Assert(State.Initialized.Value, "Contract not Initialized.");
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid param.");
            State.ProxyAccountContract.Value = input;
            return new Empty();
        }

        public override Empty Register(RegisterInput input)
        {
            ValidTokenSymbolOwner(input.ProjectSymbol, Context.Sender);
            ValidTokenSymbol(input.AcceptedSymbol);
            Assert(input.MaxSubscription >= input.MinSubscription && input.MinSubscription > 0,"Invalid subscription input");
            Assert(input.StartTime <= input.EndTime && input.StartTime > Context.CurrentBlockTime,"Invalid startTime or endTime input");
            Assert(input.TokenReleaseTime >= input.EndTime, "Invalid tokenReleaseTime input");
            Assert(input.UnlockTime >= input.EndTime, "Invalid unlockTime input");
            Assert(input.TotalPeriod <= EwellContractConstants.MaxPeriod, "Invalid totalPeriod input");
            Assert(input.FirstDistributeProportion.Add(input.TotalPeriod.Sub(1).Mul(input.RestPeriodDistributeProportion)) <= EwellContractConstants.MaxProportion,"Invalid distributeProportion input");
            var targetRaisedAmount = Parse(new BigIntValue(input.CrowdFundingIssueAmount).Mul(EwellContractConstants.Mantissa).Div(input.PreSalePrice).Value);
            Assert(targetRaisedAmount > 0, "Invalid raise amount calculated from input");
            var id = GetHash(input, Context.Sender);
            Assert( State.ProjectInfoMap[id] == null, "Project already exist");
            var virtualAddressHash = GetProjectVirtualAddressHash(Context.Sender); 
            var virtualAddress = Context.ConvertVirtualAddressToContractAddress(virtualAddressHash);
            State.ProjectAddressMap[id] = virtualAddress;
            
            TransferIn(id, Context.Sender, input.ProjectSymbol, input.CrowdFundingIssueAmount);
            State.ProjectCreatorIndexMap[Context.Sender] = State.ProjectCreatorIndexMap[Context.Sender].Add(1);
            var projectInfo = Extensions.CreateProjectInfo(input, id, Context.Sender, targetRaisedAmount, virtualAddressHash);
            State.ProjectInfoMap[id] = projectInfo;
            var listInfo = Extensions.CreateProjectListInfo(input, id);
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
                    ManagerList = new AddressList {Value = { 
                        new AddressTime {Address = Context.Sender},
                        new AddressTime {Address = Context.Self}
                    }},
                    Url = input.WhitelistUrl ?? "",
                    StrategyType = StrategyType.Basic
                });
                //write id to state and set enabled state
                Context.SendInline(Context.Self, nameof(SetWhitelistId), new SetWhitelistIdInput()
                {
                    ProjectId = id,
                    IsEnableWhitelist = input.IsEnableWhitelist
                });
            }

            Context.Fire(Extensions.GenerateProjectRegisteredEvent(input, id, Context.Sender, 
                virtualAddress, targetRaisedAmount));
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
                list.Value.Add(new AddressTime(){Address = user});
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
                list.Value.Add(new AddressTime {Address = user});
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
                    Symbol = projectInfo.ProjectSymbol,
                    Amount = toBurnAmount
                });
            }
            else
            {
                TransferOut(input, projectInfo.Creator, projectInfo.ProjectSymbol, toBurnAmount);
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
            var currentTokenReleaseTime =
                projectInfo.TokenReleaseTime.Seconds.Add(projectListInfo.PeriodDuration.Mul(projectListInfo.LatestPeriod));
            Assert(Context.CurrentBlockTime.Seconds >= currentTokenReleaseTime,"Time is not ready");
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
            Assert(projectInfo.AcceptedSymbol == input.Symbol,"The Symbol is invalid");
            CheckInvestInput(input.ProjectId, Context.Sender, input.InvestAmount);
            var currentTimestamp = Context.CurrentBlockTime;
            Assert(currentTimestamp >= projectInfo.StartTime && currentTimestamp <= projectInfo.EndTime,"Can't invest right now");
            //invest 
          
            TransferIn(input.ProjectId, Context.Sender, input.Symbol, input.InvestAmount);
            var investDetail =  State.InvestDetailMap[projectInfo.ProjectId][Context.Sender] ?? new InvestDetail()
            {
                InvestSymbol = input.Symbol,
                Amount = 0
            };
             
            var totalInvestAmount = investDetail.Amount.Add(input.InvestAmount);
            investDetail.Amount = totalInvestAmount;
            investDetail.IsDisinvest = false;
            State.InvestDetailMap[projectInfo.ProjectId][Context.Sender] = investDetail;
            State.ProjectInfoMap[input.ProjectId].CurrentRaisedAmount = State.ProjectInfoMap[input.ProjectId]
                .CurrentRaisedAmount.Add(input.InvestAmount);
            
            Assert(State.ProjectInfoMap[input.ProjectId].CurrentRaisedAmount <= projectInfo.TargetRaisedAmount,"The investment quota is already full");
            var toClaimAmount = ProfitDetailUpdate(input.ProjectId, Context.Sender, totalInvestAmount);
            
            Context.Fire(new Invested()
            {
                ProjectId = input.ProjectId,
                InvestSymbol = input.Symbol,
                Amount = input.InvestAmount,
                TotalAmount = totalInvestAmount,
                ProjectSymbol = projectInfo.ProjectSymbol,
                ToClaimAmount = toClaimAmount,
                User = Context.Sender
            });
            return new Empty();
        }

        public override Empty Disinvest(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(projectInfo.Enabled,"Project is not enabled");
            var currentTimestamp = Context.CurrentBlockTime;
            Assert(currentTimestamp >= projectInfo.StartTime && currentTimestamp <= projectInfo.EndTime,"Can't disinvest right now");
            //disinvest 
            var userinfo = State.InvestDetailMap[input][Context.Sender];
            Assert(userinfo != null,"No invest record");
            Assert(!userinfo.IsDisinvest,"User has already disinvest");
            var userAmount = userinfo.Amount;
            Assert(userAmount > 0,"Insufficient invest amount");
            State.LiquidatedDamageDetailsMap[input] =
                State.LiquidatedDamageDetailsMap[input] ?? new LiquidatedDamageDetails();
            var liquidatedDamageDetails = State.LiquidatedDamageDetailsMap[input];
            var liquidatedDamageAmountStr = new BigIntValue(userinfo.Amount).Mul(EwellContractConstants.LiquidatedDamageProportion).Div(EwellContractConstants.MaxProportion);
            var liquidatedDamageAmount = Parse(liquidatedDamageAmountStr.Value);
            var detail = new LiquidatedDamageDetail()
            {
                Amount = liquidatedDamageAmount,
                Symbol = userinfo.InvestSymbol,
                User = Context.Sender
            };

            State.InvestDetailMap[input][Context.Sender].IsDisinvest = true;
            var disinvestAmount = userAmount.Sub(liquidatedDamageAmount);
            if (disinvestAmount > 0)
            {
                TransferOut(input, Context.Sender,userinfo.InvestSymbol, disinvestAmount);
            }
            
            State.InvestDetailMap[input][Context.Sender].Amount = 0;
            State.ProjectInfoMap[input].CurrentRaisedAmount = State.ProjectInfoMap[input]
                .CurrentRaisedAmount.Sub(userAmount);
            
            ProfitDetailUpdate(input, Context.Sender, 0);
            
            Context.Fire(new DisInvested()
            {
                ProjectId = input,
                User = Context.Sender,
                InvestSymbol = userinfo.InvestSymbol,
                TotalAmount = userAmount,
                DisinvestAmount = disinvestAmount
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
            Assert(Context.CurrentBlockTime >= projectInfo.TokenReleaseTime,"Time is not ready");
            var withdrawAmount = projectInfo.CurrentRaisedAmount;
            if (withdrawAmount > 0)
            {
                TransferOut(input, projectInfo.Creator, projectInfo.AcceptedSymbol, withdrawAmount);
            }
           
            State.ProjectListInfoMap[input].IsWithdraw = true;
            var liquidatedDamageDetails = State.LiquidatedDamageDetailsMap[input];
            if (liquidatedDamageDetails != null && liquidatedDamageDetails.TotalAmount > 0)
            {
                TransferOut(input, projectInfo.Creator, projectInfo.AcceptedSymbol, liquidatedDamageDetails.TotalAmount);
            }
            var profitStr =  new BigIntValue(projectInfo.CurrentRaisedAmount).Mul(projectInfo.PreSalePrice).Div(EwellContractConstants.Mantissa).Value;
            var profit = Parse(profitStr);
            var toBurnAmount = projectInfo.CrowdFundingIssueAmount.Sub(profit);
            if (projectInfo.IsBurnRestToken)
            {
              
                State.TokenContract.Burn.VirtualSend(projectInfo.VirtualAddressHash, new BurnInput()
                {
                    Symbol = projectInfo.ProjectSymbol,
                    Amount = toBurnAmount
                });
            }
            else
            {
                TransferOut(input, projectInfo.Creator, projectInfo.ProjectSymbol, toBurnAmount);
            }
            
            Context.Fire(new Withdrawn()
            {
                ProjectId = input,
                AcceptedSymbol = projectInfo.AcceptedSymbol,
                WithdrawAmount = withdrawAmount,
                ProjectSymbol = projectInfo.ProjectSymbol,
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
                    ProjectSymbol = profitDetailInfo.Symbol,
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
            Assert(!projectInfo.Enabled, "Project should be disabled");

            var details = State.LiquidatedDamageDetailsMap[input].Details.Where(x => x.User == Context.Sender);
            foreach (var detail in details)
            {
                DoClaimLiquidatedDamage(input, detail);
            }

            return new Empty();
        }
        
        public override Empty ClaimLiquidatedDamageAll(Hash input)
        {
            var projectInfo = ValidProjectExist(input);
            Assert(!projectInfo.Enabled, "Project should be disabled");
            AdminCheck();
            var details = State.LiquidatedDamageDetailsMap[input].Details;
            foreach (var detail in details)
            {
                DoClaimLiquidatedDamage(input, detail);
            }

            return new Empty();
        }
        
        private void DoClaimLiquidatedDamage(Hash projectId, LiquidatedDamageDetail detail)
        {
            Assert(!detail.Claimed,"Already claimed");
            if (detail.Amount > 0)
            {
                TransferOut(projectId, detail.User, detail.Symbol, detail.Amount);
            }
                
            detail.Claimed = true;
            Context.Fire(new LiquidatedDamageClaimed()
            {
                ProjectId = projectId,
                Amount = detail.Amount,
                InvestSymbol = detail.Symbol,
                User = detail.User
            });
        } 
    }
}