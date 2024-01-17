using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Whitelist;
using AElf.Cryptography.ECDSA;
using AElf.Types;

namespace Ewell.Contracts.Whitelist;

public class WhitelistContractTestBase : DAppContractTestBase<WhitelistContractTestModule>
{
    protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
    protected Address DefaultAddress => Accounts[0].Address;

    protected ECKeyPair User2KeyPair => Accounts[11].KeyPair;

    protected Address User1Address => Accounts[10].Address;
    protected Address User2Address => Accounts[11].Address;
    protected Address User3Address => Accounts[14].Address;
    protected Address User4Address => Accounts[15].Address;
    protected Address User5Address => Accounts[16].Address;
    protected Address User6Address => Accounts[17].Address;


    internal WhitelistContractContainer.WhitelistContractStub WhitelistContractStub { get; set; }


    internal WhitelistContractContainer.WhitelistContractStub UserWhitelistContractStub { get; set; }


    internal Address WhitelistContractAddress => GetAddress(WhitelistSmartContractAddressNameProvider.StringName);


    public WhitelistContractTestBase()
    {
        WhitelistContractStub = GetWhitelistContractStub(DefaultKeyPair);
        UserWhitelistContractStub = GetWhitelistContractStub(User2KeyPair);
    }


    internal WhitelistContractContainer.WhitelistContractStub GetWhitelistContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<WhitelistContractContainer.WhitelistContractStub>(WhitelistContractAddress, senderKeyPair);
    }
}