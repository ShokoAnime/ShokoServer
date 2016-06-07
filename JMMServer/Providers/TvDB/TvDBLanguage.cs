using JMMContracts;

namespace JMMServer.Providers.TvDB
{
    public class TvDBLanguage
    {
        public string Name { get; set; }
        public string Abbreviation { get; set; }

        public Contract_TvDBLanguage ToContract()
        {
            var contract = new Contract_TvDBLanguage();

            contract.Abbreviation = Abbreviation;
            contract.Name = Name;

            return contract;
        }
    }
}