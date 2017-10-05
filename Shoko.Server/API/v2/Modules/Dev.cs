using Nancy;
using Nancy.Responses;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v2.Modules
{
    public class Dev : Nancy.NancyModule
    {
        public Dev() : base("/api/dev")
        {
#if DEBUG
            Get["/contracts/{entity?}"] = x => { return ExtractContracts((string) x.entity); };

#endif
        }

        /// <summary>
        /// Dumps the contracts as JSON files embedded in a zip file.
        /// </summary>
        /// <param name="entityType">The type of the entity to dump (can be <see cref="string.Empty"/> or <c>null</c> to dump all).</param>
        private object ExtractContracts(string entityType)
        {
            var zipStream = new ContractExtractor().GetContractsAsZipStream(entityType);

            return new StreamResponse(() => zipStream, "application/zip").AsAttachment("contracts.zip");
        }
    }
}