using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.Token;
using AElf.Standards.ACS0;
using AElf.Types;
using Ewell.Contracts.Whitelist;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Threading;
using WhitelistContract = Ewell.Contracts.Whitelist.WhitelistContract;

namespace Ewell.Contracts.Ido
{
    public class EwellContractTestBase : DAppContractTestBase<EwellContractTestModule>
    { 
        protected int SeedNum = 0;
        protected string SeedNFTSymbolPre = "SEED-";
        
        // You can get address of any contract via GetAddress method, for example:
        internal readonly Address EwellContractAddress;

        internal readonly Address AwakenSwapContractAddress;

        internal readonly Address LpTokentContractAddress;

        internal readonly Address WhitelistContractAddress;

        internal readonly IBlockchainService blockChainService;

        internal readonly IBlockTimeProvider blockTimeProvider;
        private Address tokenContractAddress => GetAddress(TokenSmartContractAddressNameProvider.StringName);

        internal EwellContractContainer.EwellContractStub GetEwellContractStub(
            ECKeyPair senderKeyPair)
        {
            return Application.ServiceProvider.GetRequiredService<IContractTesterFactory>()
                .Create<EwellContractContainer.EwellContractStub>(EwellContractAddress,
                    senderKeyPair);
        }
        
        internal TokenContractImplContainer.TokenContractImplStub TokenContractStub =>
            GetTokenContractStub(SampleAccount.Accounts.First().KeyPair);

        internal TokenContractImplContainer.TokenContractImplStub GetTokenContractStub(
            ECKeyPair senderKeyPair)
        {
            return Application.ServiceProvider.GetRequiredService<IContractTesterFactory>()
                .Create<TokenContractImplContainer.TokenContractImplStub>(tokenContractAddress,
                    senderKeyPair);
        }

        internal WhitelistContractContainer.WhitelistContractStub GetWhitelistContractStub(
            ECKeyPair senderKeyPair)
        {
            return Application.ServiceProvider.GetRequiredService<IContractTesterFactory>()
                .Create<WhitelistContractContainer.WhitelistContractStub>(WhitelistContractAddress, senderKeyPair);
        }

        public EwellContractTestBase()
        {
            blockChainService = Application.ServiceProvider.GetRequiredService<IBlockchainService>();
            blockTimeProvider = Application.ServiceProvider.GetRequiredService<IBlockTimeProvider>();
            EwellContractAddress = AsyncHelper.RunSync(() => DeployContractAsync(
                KernelConstants.DefaultRunnerCategory,
                File.ReadAllBytes(typeof(global::Ewell.Contracts.Ido.EwellContract).Assembly.Location),
                SampleAccount.Accounts[0].KeyPair));
            WhitelistContractAddress = AsyncHelper.RunSync(() => DeployContractAsync(
                KernelConstants.DefaultRunnerCategory,
                File.ReadAllBytes(typeof(WhitelistContract).Assembly.Location), SampleAccount.Accounts[0].KeyPair));
            
            AsyncHelper.RunSync(() => CreateSeedNftCollection(TokenContractStub));
        }

        private async Task<Address> DeployContractAsync(int category, byte[] code, ECKeyPair keyPair)
        {
            var addressService = Application.ServiceProvider.GetRequiredService<ISmartContractAddressService>();
            var stub = GetTester<ACS0Container.ACS0Stub>(addressService.GetZeroSmartContractAddress(),
                keyPair);
            var executionResult = await stub.DeploySmartContract.SendAsync(new ContractDeploymentInput
            {
                Category = category,
                Code = ByteString.CopyFrom(code)
            });
            return executionResult.Output;
        }

        private ECKeyPair AdminKeyPair { get; set; } = SampleAccount.Accounts[0].KeyPair;
        private ECKeyPair UserTomKeyPair { get; set; } = SampleAccount.Accounts.Last().KeyPair;
        private ECKeyPair UserLilyKeyPair { get; set; } = SampleAccount.Accounts.Reverse().Skip(1).First().KeyPair;

        internal Address AdminAddress => Address.FromPublicKey(AdminKeyPair.PublicKey);
        internal Address UserTomAddress => Address.FromPublicKey(UserTomKeyPair.PublicKey);

        internal EwellContractContainer.EwellContractStub AdminStub =>
            GetEwellContractStub(AdminKeyPair);

        internal EwellContractContainer.EwellContractStub TomStub =>
            GetEwellContractStub(UserTomKeyPair);

        internal TokenContractImplContainer.TokenContractImplStub TomTokenContractStub =>
            GetTokenContractStub(UserTomKeyPair);
        
        internal async Task CreateSeedNftCollection(TokenContractImplContainer.TokenContractImplStub stub)
        {
            var input = new CreateInput
            {
                Symbol = SeedNFTSymbolPre + SeedNum,
                Decimals = 0,
                IsBurnable = true,
                TokenName = "seed Collection",
                TotalSupply = 1,
                Issuer = AdminAddress,
                Owner = AdminAddress,
                ExternalInfo = new ExternalInfo()
            };
            await stub.Create.SendAsync(input);
        }
        
        internal async Task<IExecutionResult<Empty>> CreateMutiTokenAsync(
            TokenContractImplContainer.TokenContractImplStub stub,
            CreateInput createInput)
        {
            await CreateSeedNftAsync(stub, createInput);
            return await stub.Create.SendAsync(createInput);
        }
        
        internal async Task<CreateInput> CreateSeedNftAsync(TokenContractImplContainer.TokenContractImplStub stub,
            CreateInput createInput)
        {
            var input = BuildSeedCreateInput(createInput);
            await stub.Create.SendAsync(input);
            await stub.Issue.SendAsync(new IssueInput
            {
                Symbol = input.Symbol,
                Amount = 1,
                Memo = "ddd",
                To = AdminAddress
            });
            return input;
        }
        
        internal CreateInput BuildSeedCreateInput(CreateInput createInput)
        {
            Interlocked.Increment(ref SeedNum);
            var input = new CreateInput
            {
                Symbol = SeedNFTSymbolPre + SeedNum,
                Decimals = 0,
                IsBurnable = true,
                TokenName = "seed token" + SeedNum,
                TotalSupply = 1,
                Issuer = AdminAddress,
                Owner = AdminAddress,
                ExternalInfo = new ExternalInfo(),
                LockWhiteList = { TokenContractAddress }
            };
            input.ExternalInfo.Value["__seed_owned_symbol"] = createInput.Symbol;
            input.ExternalInfo.Value["__seed_exp_time"] = TimestampHelper.GetUtcNow().AddDays(1).Seconds.ToString();
            return input;
        }
        
        protected async Task<long> GetBalanceAsync(string symbol, Address owner)
        {
            var balanceResult = await TokenContractStub.GetBalance.CallAsync(
                new GetBalanceInput
                {
                    Owner = owner,
                    Symbol = symbol
                });
            return balanceResult.Balance;
        }
    }
}