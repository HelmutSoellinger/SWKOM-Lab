document.getElementById('addDocumentForm').addEventListener('submit', async (event) => {
    event.preventDefault(); // Prevent the default form submission behavior

    // Collect form data
    const formData = new FormData();
    const docName = document.getElementById('docName').value; // Document Name
    const docAuthor = document.getElementById('docAuthor').value; // Document Author
    const docDescription = document.getElementById('docDescription').value; // Document Description
    const pdfFile = document.getElementById('docContent').files[0]; // PDF File

    // Array to store client-side validation errors
    const clientSideErrors = [];

    // Client-side validation
    if (!docName) {
        clientSideErrors.push({ Property: 'Name', Message: 'Name is required.' });
    }
    if (!docAuthor) {
        clientSideErrors.push({ Property: 'Author', Message: 'Author is required.' });
    }
    if (!pdfFile) {
        clientSideErrors.push({ Property: 'Upload PDF', Message: 'A PDF file is required.' });
    }

    // If there are validation errors, display them and exit
    if (clientSideErrors.length > 0) {
        showAddSectionValidationErrors(clientSideErrors);
        return;
    }

    // Append the form fields to the FormData object
    formData.append('name', docName);
    formData.append('author', docAuthor);
    formData.append('description', docDescription || ''); // Default empty description if none provided
    formData.append('pdfFile', pdfFile); // File itself
    // The backend sets LastModified, so we don't send it here.

    // Debugging: Log the formData content (useful for development)
    formData.forEach((value, key) => {
        console.log(`FormData key: ${key}, value:`, value);
    });

    try {
        // Make the POST request to the backend API
        const response = await fetch(apiBaseUrl, {
            method: 'POST',
            body: formData,
        });

        // If the response is not OK, handle errors
        if (!response.ok) {
            const data = await response.json().catch(() => null); // Safely parse JSON if available
            if (data && data.errors) {
                showAddSectionValidationErrors(convertServerErrors(data.errors));
                throw data.errors; // Throw to skip success logic
            }
            throw new Error('Unexpected error occurred');
        }

        // If successful, reset the form and reload documents
        event.target.reset(); // Clear form inputs
        showSection('list'); // Switch to the "Document List" section
        loadDocuments(); // Refresh the document list
    } catch (errors) {
        // Handle errors and display them in the UI
        console.error('Errors:', errors);
        showAddSectionValidationErrors([
            { Property: 'Unknown', Message: 'An unexpected error occurred. Please try again.' },
        ]);
    }
});

/**
 * Converts server-side validation errors into a consistent format for the frontend.
 * @param {Object} serverErrors - Errors returned from the server.
 * @returns {Array} Array of formatted error objects.
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

    // Clear any existing errors
    addErrorList.innerHTML = '';

    // Populate the error list
    errors.forEach((error) => {
        const li = document.createElement('li');
        li.textContent = `${error.Property}: ${error.Message}`;
        addErrorList.appendChild(li);
    });

    // Show the error box
    addErrorBox.classList.remove('hidden');
    addErrorBox.style.display = 'block'; // Ensure it's visible
}

/**
 * Hides the error box for the "Add Document" section.
 */
document.getElementById('addCloseErrorBtn').addEventListener('click', () => {
    const addErrorBox = document.getElementById('addErrorMessages');
    addErrorBox.classList.add('hidden');
    addErrorBox.style.display = 'none'; // Explicitly hide it
});
