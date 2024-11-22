document.getElementById('addDocumentForm').addEventListener('submit', async (event) => {
    event.preventDefault();

    // Construct form data with document details and PDF file
    const formData = new FormData();
    const docName = document.getElementById('docName').value;
    const docAuthor = document.getElementById('docAuthor').value;
    const docDescription = document.getElementById('docDescription').value;
    const pdfFile = document.getElementById('docContent').files[0];

    // Create an array to collect validation errors
    const clientSideErrors = [];

    // Client-side validation checks
    if (!docName) {
        clientSideErrors.push({ Property: 'Name', Message: 'Name is required.' });
    }
    if (!docAuthor) {
        clientSideErrors.push({ Property: 'Author', Message: 'Author is required.' });
    }
    if (!pdfFile) {
        clientSideErrors.push({ Property: 'Upload PDF', Message: 'A PDF file is required.' });
    }

    // If there are client-side errors, display them and return
    if (clientSideErrors.length > 0) {
        showAddSectionValidationErrors(clientSideErrors);
        return;
    }

    formData.append('name', docName);
    formData.append('author', docAuthor);
    formData.append('description', docDescription);
    formData.append('pdfFile', pdfFile);

    // Debugging: Log formData values
    formData.forEach((value, key) => {
        console.log(`FormData key: ${key}, value: ${value}`);
    });

    try {
        // Post form data to the backend
        const response = await fetch(apiBaseUrl, {
            method: 'POST',
            body: formData,
        });

        if (!response.ok) {
            const data = await response.json();
            if (data.errors) {
                showAddSectionValidationErrors(convertServerErrors(data.errors));
                throw data.errors;
            }
            throw new Error('Unexpected error occurred');
        }

        // If the POST is successful, reset the form
        event.target.reset(); // Reset form on success
        showSection('list'); // Navigate to "Document List" section
        loadDocuments(); // Reload documents list

    } catch (errors) {
        console.error('Errors:', errors);
        showAddSectionValidationErrors([{ Property: 'Unknown', Message: 'An unexpected error occurred' }]);
    }
});

// Function to convert server-side errors to a standard format
function convertServerErrors(serverErrors) {
    const errorList = [];
    Object.keys(serverErrors).forEach((property) => {
        serverErrors[property].forEach((message) => {
            errorList.push({ Property: property, Message: message });
        });
    });
    return errorList;
}

// Function to show validation errors in the add section
function showAddSectionValidationErrors(errors) {
    const addErrorBox = document.getElementById('addErrorMessages');
    const addErrorList = document.getElementById('addErrorList');

    // Clear existing errors
    addErrorList.innerHTML = '';

    // Append each error to the error list
    errors.forEach((error) => {
        const li = document.createElement('li');
        li.textContent = `${error.Property}: ${error.Message}`;
        addErrorList.appendChild(li);
    });

    // Make the error box visible
    console.log("Removing 'hidden' class from error box");
    addErrorBox.classList.remove('hidden');
    addErrorBox.style.display = 'block';  // Explicitly set display to block
    console.log("Error box display style after update: ", window.getComputedStyle(addErrorBox).display);
}

// Close button to hide the error messages when needed
document.getElementById('addCloseErrorBtn').addEventListener('click', () => {
    document.getElementById('addErrorMessages').classList.add('hidden');
    document.getElementById('addErrorMessages').style.display = 'none'; // Hide the error box
});
