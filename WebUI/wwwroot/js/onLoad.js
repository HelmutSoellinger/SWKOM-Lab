document.addEventListener('DOMContentLoaded', () => {
    // Default to "Document List" view on load
    showSection('list');
    toggleVisibility(sections.add, false);
    loadDocuments();

    // Set up event listeners for navigation buttons
    document.getElementById('navList').addEventListener('click', (event) => {
        event.preventDefault();
        showSection('list');
        loadDocuments();
    });

    document.getElementById('navAdd').addEventListener('click', (event) => {
        event.preventDefault();
        showSection('add');
    });

    // Attach event listener for form submission (search functionality)
    const searchForm = document.getElementById('searchForm');
    if (searchForm) {
        searchForm.addEventListener('submit', async (event) => {
            event.preventDefault(); // Prevent the default form submission behavior
            const searchBox = document.getElementById('searchBox');
            const searchTerm = searchBox.value.trim();

            if (searchTerm) {
                // POST request to /search endpoint for term-based search
                await loadDocuments(searchTerm);
            } else {
                // GET request to fetch all documents
                await loadDocuments();
            }
        });
    } else {
        console.error("Search form with ID 'searchForm' not found.");
    }


    // Close the error modal when clicking the close button
    const closeErrorBtn = document.getElementById('addCloseErrorBtn');
    if (closeErrorBtn) {
        closeErrorBtn.addEventListener('click', hideErrorModal);
    } else {
        console.error("Element with ID 'addCloseErrorBtn' not found.");
    }
});
