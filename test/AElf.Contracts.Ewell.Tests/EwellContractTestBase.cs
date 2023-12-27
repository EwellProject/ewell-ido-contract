using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;
using System.IO;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Google.Protobuf;
using System.Linq;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Kernel.Token;
using AElf.Standards.ACS0;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Threading;
using AElf.Contracts.Whitelist;
using AElf.Kernel.Blockchain.Application;
using Awaken.Contracts.Swap;
using Awaken.Contracts.Token;


namespace AElf.Contracts.Ewell.Tests
{
    public class EwellContractTestBase : DAppContractTestBase<EwellContractTestModule>
    {
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
                .Create<AElf.Contracts.Ewell.EwellContractContainer.EwellContractStub>(EwellContractAddress,
                    senderKeyPair);
        }

        internal AwakenSwapContractContainer.AwakenSwapContractStub GetAwakenSwapContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<AwakenSwapContractContainer.AwakenSwapContractStub>(AwakenSwapContractAddress,
                senderKeyPair);
        }


        internal AwakenSwapContractContainer.AwakenSwapContractStub AwakenSwapContractStub =>
            GetAwakenSwapContractStub(SampleAccount.Accounts.First().KeyPair);

        internal AElf.Contracts.MultiToken.TokenContractContainer.TokenContractStub TokenContractStub =>
            GetTokenContractStub(SampleAccount.Accounts.First().KeyPair);

        internal AElf.Contracts.MultiToken.TokenContractContainer.TokenContractStub GetTokenContractStub(
            ECKeyPair senderKeyPair)
        {
            return Application.ServiceProvider.GetRequiredService<IContractTesterFactory>()
                .Create<AElf.Contracts.MultiToken.TokenContractContainer.TokenContractStub>(tokenContractAddress,
                    senderKeyPair);
        }

        internal TokenContractContainer.TokenContractStub GetLpContractStub(
            ECKeyPair senderKeyPair)
        {
            return Application.ServiceProvider.GetRequiredService<IContractTesterFactory>()
                .Create<TokenContractContainer.TokenContractStub>(LpTokentContractAddress, senderKeyPair);
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
                File.ReadAllBytes(typeof(EwellContract).Assembly.Location),
                SampleAccount.Accounts[0].KeyPair));
            AwakenSwapContractAddress = AsyncHelper.RunSync(() => DeployContractAsync(
                KernelConstants.DefaultRunnerCategory,
                File.ReadAllBytes(typeof(AwakenSwapContract).Assembly.Location), SampleAccount.Accounts[0].KeyPair));
            LpTokentContractAddress = AsyncHelper.RunSync(() => DeployContractAsync(
                KernelConstants.DefaultRunnerCategory,
                File.ReadAllBytes(typeof(TokenContract).Assembly.Location), SampleAccount.Accounts[0].KeyPair));
            WhitelistContractAddress = AsyncHelper.RunSync(() => DeployContractAsync(
                KernelConstants.DefaultRunnerCategory,
                File.ReadAllBytes(typeof(WhitelistContract).Assembly.Location), SampleAccount.Accounts[0].KeyPair));
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
        internal Address UserLilyAddress => Address.FromPublicKey(UserLilyKeyPair.PublicKey);


        internal EwellContractContainer.EwellContractStub AdminStub =>
            GetEwellContractStub(AdminKeyPair);

        internal EwellContractContainer.EwellContractStub TomStub =>
            GetEwellContractStub(UserTomKeyPair);

        internal MultiToken.TokenContractContainer.TokenContractStub TomTokenContractStub =>
            GetTokenContractStub(UserTomKeyPair);


        internal Awaken.Contracts.Token.TokenContractContainer.TokenContractStub AdminLpStub =>
            GetLpContractStub(AdminKeyPair);

        internal AwakenSwapContractContainer.AwakenSwapContractStub UserTomSwapStub =>
            GetAwakenSwapContractStub(UserTomKeyPair);
    }
}