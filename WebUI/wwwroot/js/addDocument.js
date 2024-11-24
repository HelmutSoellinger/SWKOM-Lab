document.getElementById('addDocumentForm').addEventListener('submit', async (event) => {
    event.preventDefault(); // Prevent the default form submission behavior

    // Collect form data
    const formData = new FormData();
    const docName = document.getElementById('docName').value.trim(); // Document Name
    const docAuthor = document.getElementById('docAuthor').value.trim(); // Document Author
    const docDescription = document.getElementById('docDescription').value.trim() || ""; // Document Description
    const pdfFile = document.getElementById('docFile').files[0]; // PDF File

    // Array to store client-side validation errors
    const clientSideErrors = [];

    // Client-side validation matching backend
    if (!docName) {
        clientSideErrors.push({ Property: 'Name', Message: 'Name is required.' });
    } else if (docName.length > 100) {
        clientSideErrors.push({ Property: 'Name', Message: 'Name cannot exceed 100 characters.' });
    }

    if (!docAuthor) {
        clientSideErrors.push({ Property: 'Author', Message: 'Author is required.' });
    } else if (docAuthor.length > 100) {
        clientSideErrors.push({ Property: 'Author', Message: 'Author cannot exceed 100 characters.' });
    }

    if (!pdfFile) {
        clientSideErrors.push({ Property: 'Upload PDF', Message: 'A PDF file is required.' });
    }

    if (docDescription.length > 500) {
        clientSideErrors.push({
            Property: 'Description',
            Message: 'Description cannot exceed 500 characters.',
        });
    }

    // If there are validation errors, display them and exit
    if (clientSideErrors.length > 0) {
        showAddSectionValidationErrors(clientSideErrors);
        return;
    }

    // Append validated fields to FormData
    formData.append('name', docName);
    formData.append('author', docAuthor);
    formData.append('description', docDescription);
    formData.append('pdfFile', pdfFile);

    try {
        // Send validated data to the backend
        const response = await fetch(apiBaseUrl, {
            method: 'POST',
            body: formData,
        });

        if (!response.ok) {
            const data = await response.json().catch(() => null); // Safely parse JSON if available
            if (data && data.errors) {
                showAddSectionValidationErrors(convertServerErrors(data.errors));
                throw data.errors; // Skip success logic
            }
            throw new Error('Unexpected error occurred');
        }

        // Reset form and refresh list on success
        event.target.reset(); // Clear form inputs
        showSection('list'); // Switch to the "Document List" section
        loadDocuments(); // Refresh the document list
        alert('Document successfully added!'); // Show confirmation message
    } catch (errors) {
        console.error('Errors:', errors);
        showAddSectionValidationErrors([
            { Property: 'Unknown', Message: 'An unexpected error occurred. Please try again.' },
        ]);
    }
});

/**
 * Converts backend validation errors into a consistent frontend format.
 * @param {Object} serverErrors - Errors returned from the backend.
 * @returns {Array} Formatted error objects.
 */
function convertServerErrors(serverErrors) {
    const errorList = [];
    Object.keys(serverErrors).forEach((property) => {
        serverErrors[property].forEach((message) => {
            errorList.push({ Property: property, Message: message });
        });
    });
    return errorList;
}

/**
 * Displays validation errors in the "Add Document" section.
 * @param {Array} errors - Array of error objects with `Property` and `Message` fields.
 */
function showAddSectionValidationErrors(errors) {
    const addErrorBox = document.getElementById('addErrorMessages');
    const addErrorList = document.getElementById('addErrorList');

    addErrorList.innerHTML = ''; // Clear any existing errors

    errors.forEach((error) => {
        const li = document.createElement('li');
        li.textContent = `${error.Property}: ${error.Message}`;
        addErrorList.appendChild(li);
    });

    addErrorBox.classList.remove('hidden');
    addErrorBox.style.display = 'block'; // Ensure visibility
}

/**
 * Hides the error box for the "Add Document" section.
 */
document.getElementById('addCloseErrorBtn').addEventListener('click', () => {
    const addErrorBox = document.getElementById('addErrorMessages');
    addErrorBox.classList.add('hidden');
    addErrorBox.style.display = 'none';
});
