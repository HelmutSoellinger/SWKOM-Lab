async function loadDocuments(searchTerm = '') {
    const tableBody = document.querySelector('#documentsTable tbody');
    tableBody.innerHTML = ''; // Clear the table

    try {
        let documents = [];
        let response;

        if (searchTerm.trim() === '') {
            // Fetch all documents if no search term
            response = await fetch(apiBaseUrl); // GET all documents
        } else {
            // Search using the POST search endpoint
            response = await fetch(`${apiBaseUrl}/search`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(searchTerm.trim()), // Send trimmed search term in body
            });
        }

        if (!response.ok) {
            if (response.status === 404) {
                // Handle no search results
                tableBody.innerHTML = `<tr><td colspan="5">No matching documents found.</td></tr>`;
                return;
            }
            throw new Error('Failed to fetch documents');
        }

        documents = await response.json();

        if (!Array.isArray(documents) || documents.length === 0) {
            // Show "No documents found" if the response is empty
            tableBody.innerHTML = `<tr><td colspan="5">No documents found.</td></tr>`;
            return;
        }

        // Populate the table with documents
        documents.forEach((doc) => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${doc.id || 'N/A'}</td>
                <td>${doc.name || 'N/A'}</td>
                <td>${doc.author || 'N/A'}</td>
                <td>${formatLastModifiedDate(doc.lastModified)}</td>
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

function formatLastModifiedDate(lastModified) {
    if (!lastModified) return 'N/A';
    const date = new Date(lastModified);
    return date.toLocaleDateString('de-DE');
}
