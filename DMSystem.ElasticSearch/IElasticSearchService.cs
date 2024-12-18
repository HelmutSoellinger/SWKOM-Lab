﻿using DMSystem.Contracts;

namespace DMSystem.ElasticSearch
{
    public interface IElasticSearchService
    {
        Task IndexDocumentAsync(OCRResult ocrResult);
        Task<IEnumerable<OCRResult>> SearchDocumentsAsync(string searchTerm);
        Task DeleteDocumentByIdAsync(int documentId); // Add this method
    }
}
