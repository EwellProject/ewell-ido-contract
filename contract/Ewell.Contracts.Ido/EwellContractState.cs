using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Ewell.Contracts.Ido
{
    public partial class EwellContractState : ContractState
    {
        public SingletonState<Address> Admin { get; set; }

        public MappedState<Hash, ProjectInfo> ProjectInfoMap { get; set; }

        public MappedState<Hash, ProjectListInfo> ProjectListInfoMap { get; set; }

        public MappedState<Hash, Address, InvestDetail> InvestDetailMap { get; set; }

        public MappedState<Hash, Address, ProfitDetail> ProfitDetailMap { get; set; }

        public MappedState<Address, ClaimedProfitsInfo> ClaimedProfitsInfoMap { get; set; }

        public MappedState<Hash, LiquidatedDamageDetails> LiquidatedDamageDetailsMap { get; set; }

        public MappedState<Hash, Hash> WhiteListIdMap { get; set; }

        public MappedState<Hash, Address> ProjectAddressMap { get; set; }

        public MappedState<Address, int> ProjectCreatorIndexMap { get; set; }
    }
}