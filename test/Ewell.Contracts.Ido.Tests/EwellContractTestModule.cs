using AElf.Boilerplate.TestBase;
using AElf.Kernel.SmartContract;
using Volo.Abp.Modularity;

namespace Ewell.Contracts.Ido
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