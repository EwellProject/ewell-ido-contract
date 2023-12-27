using AElf.Boilerplate.TestBase;
using AElf.Kernel.SmartContract;
using Volo.Abp.Modularity;

namespace AElf.Contracts.Ewell.Tests
{
    [DependsOn(typeof(MainChainDAppContractTestModule))]
    public class EwellContractTestModule: MainChainDAppContractTestModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<ContractOptions>(o=>o.ContractDeploymentAuthorityRequired = false);
        }
    }
}