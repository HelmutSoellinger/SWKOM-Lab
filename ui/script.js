const sections = {
    list: document.getElementById('listSection'),
    add: document.getElementById('addSection'),
};

// Update this URL to your NGINX or API URL
const apiUrl = 'http://localhost/Document';  // Proxy via NGINX to avoid port issues

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
    let url = apiUrl + (filterName ? `?name=${filterName}` : '');

    console.log('Fetching documents from:', url); // Log the URL being fetched

    fetch(url)
        .then((response) => {
            console.log('Response status:', response.status); // Log the response status
            if (!response.ok) {
                throw new Error(`Error fetching documents: ${response.statusText}`);
            }
            return response.json();
        })
        .then((documents) => {
            console.log('Documents:', documents); // Log the documents received
            const tableBody = document.querySelector('#documentsTable tbody');
            tableBody.innerHTML = ''; // Clear existing table rows

            if (documents.length === 0) {
                tableBody.innerHTML = '<tr><td colspan="6">No documents found</td></tr>';
                return;
            }

            documents.forEach((doc) => {
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td>${doc.id}</td>
                    <td>${doc.name}</td>
                    <td>${doc.author}</td>
                    <td>${doc.lastModified}</td>
                    <td>${doc.description}</td>
                    <td><button onclick="deleteDocument(${doc.id})">Delete</button></td>
                `;
                tableBody.appendChild(row);
            });
        })
        .catch((error) => {
            console.error('Error loading documents:', error);
        });
}

// Helper function to format dates
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString();  // Adjust date format if needed
}

// Filter documents by name
document.getElementById('filterBtn').addEventListener('click', () => {
    const filterName = document.getElementById('filterName').value;
    loadDocuments(filterName);
});

// Add new document
document.getElementById('addDocumentForm').addEventListener('submit', (event) => {
    event.preventDefault();

    const newDocument = {
        name: document.getElementById('docName').value,
        author: document.getElementById('docAuthor').value,
        description: document.getElementById('docDescription').value,
        content: document.getElementById('docContent').value,
        lastModified: new Date().toISOString().split('T')[0],  // Format for date
    };

    fetch(apiUrl, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(newDocument),
    })
    .then(() => loadDocuments())
    .catch((error) => console.error('Error adding document:', error));
});

// Delete document
function deleteDocument(id) {
    fetch(`${apiUrl}/${id}`, { method: 'DELETE' })
        .then(() => loadDocuments())
        .catch((error) => console.error('Error deleting document:', error));
}
