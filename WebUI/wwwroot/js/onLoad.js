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

    // Close the error modal when clicking the close button
    const closeErrorBtn = document.getElementById('addCloseErrorBtn'); // Updated to match your latest changes
    if (closeErrorBtn) {
        closeErrorBtn.addEventListener('click', hideErrorModal);
    } else {
        console.error("Element with ID 'addCloseErrorBtn' not found."); // Error message adjusted to match new ID
    }
});
