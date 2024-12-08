using DMSystem.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSystem.ElasticSearch
{
    public interface IElasticSearchService
    {
        Task IndexDocumentAsync(OCRResult ocrResult);
        Task<IEnumerable<SearchResult>> SearchDocumentsAsync(string searchTerm);
    }
}
