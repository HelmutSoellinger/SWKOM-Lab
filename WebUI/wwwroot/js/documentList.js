function loadDocuments(filterName = '') {
    // Construct the URL depending on whether filterName is provided
    const url = filterName ? `${apiBaseUrl}?name=${filterName}` : apiBaseUrl;

    fetch(url)
        .then((response) => {
            if (!response.ok) throw new Error('Failed to fetch documents');
            return response.json();
        })
        .then((documents) => {
            const tableBody = document.querySelector('#documentsTable tbody');
            tableBody.innerHTML = ''; // Clear the table

            documents.forEach((doc) => {
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td>${doc.id}</td>
                    <td>${doc.name}</td>
                    <td>${doc.author}</td>
                    <td>${doc.lastModified}</td>
                    <td>${doc.description || ''}</td>
                    <td class="actions">
                        <a class="download-btn" href="${apiBaseUrl}/${doc.id}/download" target="_blank">Download</a>
                        <button class="delete-btn" data-id="${doc.id}">X</button>
                    </td>
                `;
                tableBody.appendChild(row);
            });

            // Attach event listeners to delete buttons
            document.querySelectorAll('.delete-btn').forEach(button => {
                button.addEventListener('click', () => {
                    const documentId = button.dataset.id;
                    if (documentId) {
                        deleteDocument(documentId);
                    }
                });
            });
        })
        .catch(() => {
            showErrorModal([{ Property: 'Network', Message: 'Error loading documents' }]);
        });
}

/**
 * Deletes a document by ID using the API endpoint.
 * @param {string} documentId The ID of the document to delete.
 */
function deleteDocument(documentId) {
    const deleteUrl = `${apiBaseUrl}/${documentId}`;

    fetch(deleteUrl, { method: 'DELETE' })
        .then((response) => {
            if (!response.ok) {
                if (response.status === 404) {
                    showErrorModal([{ Property: 'Document', Message: 'Document not found' }]);
                } else {
                    throw new Error('Failed to delete document');
                }
            } else {
                // Reload the list after successful deletion
                loadDocuments();
            }
        })
        .catch(() => {
            showErrorModal([{ Property: 'Network', Message: 'Error deleting document' }]);
        });
}
