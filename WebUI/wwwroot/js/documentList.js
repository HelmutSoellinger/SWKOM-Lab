/**
 * Loads documents from the API and populates the table.
 * @param {string} filterName Optional filter for document name.
 */
async function loadDocuments(filterName = '') {
    const url = filterName ? `${apiBaseUrl}?name=${filterName}` : apiBaseUrl;

    try {
        const response = await fetch(url);

        if (!response.ok) {
            throw new Error('Failed to fetch documents');
        }

        const documents = await response.json();
        const tableBody = document.querySelector('#documentsTable tbody');
        tableBody.innerHTML = ''; // Clear the table

        documents.forEach((doc) => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${doc.id}</td>
                <td>${doc.name}</td>
                <td>${doc.author}</td>
                <td>${formatLastModifiedDate(doc.lastModified)}</td>
                <td>${doc.description === null ? 'N/A' : doc.description || ''}</td>
                <td class="actions">
                    <a class="download-btn" href="${apiBaseUrl}/${doc.id}/download" target="_blank">Download</a>
                    <button class="delete-btn" data-id="${doc.id}">X</button>
                </td>
            `;
            tableBody.appendChild(row);
        });

        // Attach event listeners to delete buttons
        document.querySelectorAll('.delete-btn').forEach((button) => {
            button.addEventListener('click', () => {
                const documentId = button.dataset.id;
                if (documentId) {
                    deleteDocument(documentId);
                }
            });
        });
    } catch (error) {
        console.error('Error loading documents:', error);
        showErrorModal([{ Property: 'Network', Message: 'Error loading documents' }]);
    }
}

/**
 * Deletes a document by ID using the API endpoint.
 * @param {string} documentId The ID of the document to delete.
 */
async function deleteDocument(documentId) {
    const deleteUrl = `${apiBaseUrl}/${documentId}`;

    try {
        const response = await fetch(deleteUrl, { method: 'DELETE' });

        if (!response.ok) {
            if (response.status === 404) {
                showErrorModal([{ Property: 'Document', Message: 'Document not found' }]);
            } else {
                throw new Error('Failed to delete document');
            }
        } else {
            // Reload the list after successful deletion
            await loadDocuments();
        }
    } catch (error) {
        console.error('Error deleting document:', error);
        showErrorModal([{ Property: 'Network', Message: 'Error deleting document' }]);
    }
}

/**
 * Formats the LastModified date to DD.MM.YYYY.
 * @param {string} lastModified ISO date string from the API.
 * @returns {string} Formatted date in DD.MM.YYYY format.
 */
function formatLastModifiedDate(lastModified) {
    if (!lastModified) return 'N/A'; // Handle null or undefined values
    const date = new Date(lastModified);
    return date.toLocaleDateString('de-DE'); // 'de-DE' for DD.MM.YYYY format
}
