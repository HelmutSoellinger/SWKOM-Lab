const apiBaseUrl = 'http://localhost/api/Document'; // Base URL for the Document API

const sections = {
    list: document.getElementById('listSection'),
    add: document.getElementById('addSection'),
};

const errorBox = document.getElementById('addErrorMessages'); // Updated to match HTML
const errorList = document.getElementById('addErrorList'); // Updated to match HTML
const closeErrorBtn = document.getElementById('addCloseErrorBtn'); // Make sure the ID is correct here

// Utility function to show/hide elements
function toggleVisibility(element, isVisible) {
    if (element) {
        if (isVisible) {
            element.classList.remove('hidden');
        } else {
            element.classList.add('hidden');
        }
    } else {
        console.error("Element not found for toggleVisibility function.");
    }
}

// Show the specified section and hide all others
function showSection(section) {
    Object.keys(sections).forEach((key) => {
        toggleVisibility(sections[key], key === section);
    });
    hideErrorModal(); // Always hide the error box when switching sections
}

// Hide the error box
function hideErrorModal() {
    if (errorBox) {
        toggleVisibility(errorBox, false);
        if (errorList) {
            errorList.innerHTML = ''; // Clear previous errors
        }
    } else {
        console.error("Error box element not found for hideErrorModal function.");
    }
}

// Show the error box with errors
function showErrorModal(errors) {
    if (errorList) {
        errorList.innerHTML = ''; // Clear existing errors
        errors.forEach((error) => {
            const li = document.createElement('li');
            li.textContent = `${error.Property}: ${error.Message}`;
            errorList.appendChild(li);
        });
        toggleVisibility(errorBox, true); // Display the error box
    } else {
        console.error("Error list element not found for showErrorModal function.");
    }
}
