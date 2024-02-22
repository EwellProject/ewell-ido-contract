using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Volo.Abp.DependencyInjection;

namespace Ewell.Contracts.Whitelist;

public class WhitelistContractInitializationProvider : IContractInitializationProvider, ISingletonDependency
{
    public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
    {
        return new List<ContractInitializationMethodCall>();
    }

    public Hash SystemSmartContractName => WhitelistSmartContractAddressNameProvider.Name;
    public string ContractCodeName => "Ewell.Whitelist";
}