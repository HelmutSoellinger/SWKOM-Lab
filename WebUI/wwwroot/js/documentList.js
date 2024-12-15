async function loadDocuments(searchTerm = '') {
    const tableBody = document.querySelector('#documentsTable tbody');
    tableBody.innerHTML = ''; // Clear the table

    try {
        let response;

        if (searchTerm.trim() === '') {
            response = await fetch(apiBaseUrl); // Fetch all documents
        } else {
            response = await fetch(`${apiBaseUrl}/search`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(searchTerm.trim()),
            });
        }

        if (!response.ok) {
            handleFetchError(response);
            return;
        }

        const documents = await response.json();
        if (!Array.isArray(documents) || documents.length === 0) {
            tableBody.innerHTML = `<tr><td colspan="5">No documents found.</td></tr>`;
            return;
        }

        documents.forEach((doc) => {
            // Handle both flat and nested structures
            const documentData = doc.document || doc;

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${documentData.id || 'N/A'}</td>
                <td>${documentData.name || 'N/A'}</td>
                <td>${documentData.author || 'N/A'}</td>
                <td>${formatLastModifiedDate(documentData.lastModified)}</td>
                <td class="actions">
                    <button class="editButton" data-id="${documentData.id}">Edit</button>
                    <a class="download-btn" href="${apiBaseUrl}/${documentData.id}/download" target="_blank">Download</a>
                    <button class="delete-btn" data-id="${documentData.id}">X</button>
                </td>
            `;
            tableBody.appendChild(row);
        });

        // Attach events to buttons
        attachEditButtonEvents();
        attachDeleteButtonEvents();
    } catch (error) {
        console.error('Error loading documents:', error);
        alert('Error loading documents. Please try again later.');
    }
}

// Attach event listeners for Edit buttons
function attachEditButtonEvents() {
    document.querySelectorAll('.editButton').forEach((button) => {
        button.addEventListener('click', async () => {
            const documentId = button.dataset.id;
            try {
                const response = await fetch(`${apiBaseUrl}/${documentId}`);
                if (!response.ok) {
                    throw new Error('Failed to fetch document details');
                }
                const document = await response.json();
                openEditModal(document);
            } catch (error) {
                console.error('Error loading document:', error);
                alert('Failed to load document for editing.');
            }
        });
    });
}

// Attach event listeners for Delete buttons
function attachDeleteButtonEvents() {
    document.querySelectorAll('.delete-btn').forEach((button) => {
        button.addEventListener('click', () => {
            const documentId = button.dataset.id;
            if (documentId) {
                deleteDocument(documentId);
            }
        });
    });
}

// Function to handle document deletion
async function deleteDocument(documentId) {
    const deleteUrl = `${apiBaseUrl}/${documentId}`;

    try {
        const response = await fetch(deleteUrl, { method: 'DELETE' });
        if (!response.ok) {
            if (response.status === 404) {
                alert('Document not found');
            } else {
                throw new Error('Failed to delete document');
            }
        }
        loadDocuments(); // Reload the list
    } catch (error) {
        console.error('Error deleting document:', error);
        alert('An error occurred while deleting the document.');
    }
}

// Open the edit modal and pre-fill fields
function openEditModal(doc) {
    const editModal = document.getElementById('editDocumentModal');
    const editForm = document.getElementById('editDocumentForm');
    const editDocName = document.getElementById('editDocName');
    const editDocAuthor = document.getElementById('editDocAuthor');

    editDocName.value = doc.name || '';
    editDocAuthor.value = doc.author || '';
    editForm.dataset.id = doc.id;

    // Show modal
    editModal.classList.remove('hidden');
    editModal.style.display = 'block';

    // Clean up old event listeners before attaching new ones
    editForm.removeEventListener('submit', submitEditForm);
    editForm.addEventListener('submit', submitEditForm);
}

// Submit updated document data
async function submitEditForm(e) {
    e.preventDefault();

    const editForm = e.target;
    const documentId = editForm.dataset.id;

    const formData = new FormData();
    formData.append('name', document.getElementById('editDocName').value.trim());
    formData.append('author', document.getElementById('editDocAuthor').value.trim());

    // Attach the file if selected
    const fileInput = document.getElementById('editDocFile');
    if (fileInput.files.length > 0) {
        formData.append('pdfFile', fileInput.files[0]);
    }

    try {
        const response = await fetch(`${apiBaseUrl}/${documentId}/upload`, {
            method: 'PUT',
            body: formData, // multipart/form-data
        });

        if (!response.ok) {
            throw new Error('Failed to update document');
        }

        alert('Document successfully updated!');
        loadDocuments(); // Refresh the document list
        closeEditModal();
    } catch (error) {
        console.error('Error updating document:', error);
        alert('An unexpected error occurred while updating the document.');
    }
}

// Close the edit modal
function closeEditModal() {
    const editModal = document.getElementById('editDocumentModal');
    editModal.classList.add('hidden');
    editModal.style.display = 'none';
}

// Format the last modified date
function formatLastModifiedDate(lastModified) {
    if (!lastModified) return 'N/A';
    const date = new Date(lastModified);
    return date.toLocaleDateString('de-DE');
}

// Handle fetch errors
async function handleFetchError(response) {
    const errorMessage = await response.text();
    console.error('Response Error:', errorMessage);
    alert(`Error: ${response.status} - ${errorMessage}`);
}
