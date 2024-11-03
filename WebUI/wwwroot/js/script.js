document.addEventListener('DOMContentLoaded', () => {
    const sections = {
        list: document.getElementById('listSection'),
        add: document.getElementById('addSection'),
    };

    // Base API URL
    const apiBaseUrl = 'http://localhost/api/Document'; // Use the service name

    // Load documents when the website loads
    loadDocuments();

    // Navbar Event Listeners
    document.getElementById('navList').addEventListener('click', (event) => {
        event.preventDefault();
        showSection('list');
        loadDocuments();
    });

    document.getElementById('navAdd').addEventListener('click', (event) => {
        event.preventDefault();
        showSection('add');
    });

    document.getElementById('homeBtn').addEventListener('click', () => {
        showSection('list');
        loadDocuments();
    });

    // Show specified section
    function showSection(section) {
        Object.keys(sections).forEach((key) => {
            sections[key].style.display = key === section ? 'block' : 'none';
        });
    }

    // Load documents from API
    function loadDocuments(filterName = '') {
        let url = apiBaseUrl + (filterName ? `?name=${filterName}` : '');

        fetch(url)
            .then((response) => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then((documents) => {
                const tableBody = document.querySelector('#documentsTable tbody');
                tableBody.innerHTML = ''; // Clear the table body

                documents.forEach((doc) => {
                    const row = document.createElement('tr');
                    row.innerHTML = `
                        <td>${doc.id}</td>
                        <td>${doc.name}</td>
                        <td>${doc.author}</td>
                        <td>${doc.lastModified}</td>
                        <td>${doc.description}</td>
                        <td>
                            <a href="${apiBaseUrl}/${doc.id}/download" target="_blank">Download</a>
                            <button onclick="deleteDocument(${doc.id})">Delete</button>
                        </td>
                    `;
                    tableBody.appendChild(row);
                });
            })
            .catch((error) => {
                console.error('Error loading documents:', error);
                alert('Error loading documents. Please try again later.');
            });
    }

    // Filter documents by name
    document.getElementById('filterBtn').addEventListener('click', () => {
        const filterName = document.getElementById('filterName').value;
        loadDocuments(filterName);
    });

    // Add new document
    document.getElementById('addDocumentForm').addEventListener('submit', (event) => {
        event.preventDefault();

        const formData = new FormData();
        formData.append('name', document.getElementById('docName').value);
        formData.append('author', document.getElementById('docAuthor').value);
        formData.append('description', document.getElementById('docDescription').value);
        formData.append('pdfFile', document.getElementById('docContent').files[0]); // Handle file upload

        fetch(apiBaseUrl, {
            method: 'POST',
            body: formData,
        })
            .then(response => {
                if (!response.ok) {
                    // Log the response status and attempt to parse JSON body for error details
                    console.error('Response status:', response.status);
                    return response.json().then((data) => {
                        console.log('Full error response:', data); // Log full error response for debugging
                        if (data.errors) {
                            throw data.errors; // Pass the validation errors to the catch block
                        }
                        throw new Error('An unexpected error occurred.');
                    });
                }
                return response.json();
            })
            .then(() => {
                // Successfully added document, reset form, show list, and reload documents
                document.getElementById('addDocumentForm').reset();
                showSection('list');
                loadDocuments();
                alert('Document added successfully!');
            })
            .catch((errors) => {
                if (Array.isArray(errors)) {
                    // Display validation errors
                    let errorMessages = 'Please fix the following errors:\n';
                    errors.forEach(error => {
                        errorMessages += `• ${error.Property}: ${error.Message}\n`;
                    });
                    alert(errorMessages);
                } else {
                    // Handle unexpected errors
                    console.error('Error adding document:', errors);
                    alert('Error adding document. Please try again later.');
                }
            });
    });

    // Delete document
    window.deleteDocument = function (id) {
        if (confirm('Are you sure you want to delete this document?')) {
            fetch(`${apiBaseUrl}/${id}`, { method: 'DELETE' })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Network response was not ok');
                    }
                    loadDocuments(); // Reload documents after deletion
                })
                .catch((error) => {
                    console.error('Error deleting document:', error);
                    alert('Error deleting document. Please try again later.');
                });
        }
    }
});