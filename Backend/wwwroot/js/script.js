document.addEventListener('DOMContentLoaded', () => {
    const chatMessages = document.getElementById('chatMessages');
    const userInput = document.getElementById('userInput');
    const sendButton = document.getElementById('sendButton');
    const clearChatButton = document.getElementById('clearChatButton');
    const downloadChatButton = document.getElementById('downloadChatButton');
    const clearDocumentButton = document.getElementById('clearDocumentButton');
    const fileInput = document.getElementById('fileInput');
    const uploadButton = document.getElementById('uploadButton');
    
    // Variable to track if document context is active
    let documentContextActive = false;

    // Store the current file for submission with the next message
    let currentFile = null;
    let currentFileName = null;

    // Auto-resize the textarea as the user types
    userInput.addEventListener('input', () => {
        userInput.style.height = 'auto';
        userInput.style.height = (userInput.scrollHeight) + 'px';
    });
    
    // Clear chat functionality
    clearChatButton.addEventListener('click', () => {
        // Keep only the first welcome message
        const welcomeMessage = chatMessages.querySelector('.bot-message');
        chatMessages.innerHTML = '';
        chatMessages.appendChild(welcomeMessage);
    });
    
    // Download chat functionality
    downloadChatButton.addEventListener('click', () => {
        // Create text content from chat messages
        let chatContent = '';
        const messages = chatMessages.querySelectorAll('.message');
        
        messages.forEach(message => {
            const isBot = message.classList.contains('bot-message');
            const content = message.textContent.trim();
            chatContent += `${isBot ? 'RAI: ' : 'You: '}${content}\n\n`;
        });
        
        // Create a blob and download link
        const blob = new Blob([chatContent], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `rai-chat-${new Date().toISOString().slice(0, 10)}.txt`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    });
    
    // Clear document context functionality
    clearDocumentButton.addEventListener('click', async () => {
        try {
            // Call the clear document context API
            const response = await fetch('/api/chat/clear-document', {
                method: 'POST'
            });
            
            if (!response.ok) {
                throw new Error('Error clearing document context');
            }
            
            const data = await response.json();
            
            // Update UI to show document context is cleared
            documentContextActive = false;
            updateDocumentContextUI();
            
            // Add system message to chat
            addMessage("Document context has been cleared. I'm no longer using any document for context.", 'system');
            
        } catch (error) {
            console.error('Error:', error);
            addMessage("Sorry, there was an error clearing the document context.", 'system');
        }
    });
    
    // Function to update UI based on document context status
    function updateDocumentContextUI() {
        if (documentContextActive) {
            clearDocumentButton.classList.add('active');
            clearDocumentButton.setAttribute('title', 'Clear Document Context (Active)');
        } else {
            clearDocumentButton.classList.remove('active');
            clearDocumentButton.setAttribute('title', 'Clear Document Context (None)');
        }
    }
    
    // Initialize UI
    updateDocumentContextUI();
    
    // File upload functionality
    fileInput.addEventListener('change', (e) => {
        const file = e.target.files[0];
        if (!file) return;
        
        // Store the file for the next message
        currentFile = file;
        currentFileName = file.name;
        
        // Show notification that file is ready to be sent
        addMessage(`File "${file.name}" is ready to be sent with your next message.`, 'system');
        
        // Update UI to show a file is attached
        uploadButton.classList.add('active');
        uploadButton.setAttribute('title', `File attached: ${file.name}`);
        
        // Focus on the message input
        userInput.focus();
    });

    // Send message when the send button is clicked
    sendButton.addEventListener('click', sendMessage);

    // Send message when Enter key is pressed (but allow Shift+Enter for new lines)
    userInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    async function sendMessage() {
        const message = userInput.value.trim();
        if (message === '') return;

        // Add user message to chat
        if (currentFile) {
            addMessage(`${message} [with file: ${currentFileName}]`, 'user');
        } else {
            addMessage(message, 'user');
        }

        // Clear input and reset height
        userInput.value = '';
        userInput.style.height = 'auto';

        // Show typing indicator
        showTypingIndicator();

        try {
            let response;
            
            // Check if we have a file to send with the message
            if (currentFile) {
                // Create form data with both file and message
                const formData = new FormData();
                formData.append('file', currentFile);
                formData.append('message', message);
                
                // Call the chat-with-file API
                const fileResponse = await fetch('/api/chat/with-file', {
                    method: 'POST',
                    body: formData
                });
                
                if (!fileResponse.ok) {
                    throw new Error('Error processing file with message');
                }
                
                const data = await fileResponse.json();
                response = data.response;
                
                // Reset file attachment
                currentFile = null;
                currentFileName = null;
                uploadButton.classList.remove('active');
                uploadButton.setAttribute('title', 'Upload Document');
                fileInput.value = '';
            } else {
                // Regular chat without file
                response = await callChatAPI(message);
            }
            
            // Remove typing indicator
            removeTypingIndicator();
            
            // Add bot response to chat
            addMessage(response, 'bot');
        } catch (error) {
            // Remove typing indicator
            removeTypingIndicator();
            
            // Show error message
            addMessage("Sorry, there was an error processing your request. Please try again later.", 'bot');
            console.error('Error:', error);
            
            // Reset file attachment on error
            if (currentFile) {
                currentFile = null;
                currentFileName = null;
                uploadButton.classList.remove('active');
                uploadButton.setAttribute('title', 'Upload Document');
                fileInput.value = '';
            }
        }
    }

    function addMessage(content, sender) {
        const messageDiv = document.createElement('div');
        messageDiv.classList.add('message', `${sender}-message`);
        
        const messageContent = document.createElement('div');
        messageContent.classList.add('message-content');
        
        // Format bot and system messages with better styling
        if (sender === 'bot' || sender === 'system') {
            // Convert line breaks to paragraphs
            const paragraphs = content.split('\n').filter(p => p.trim() !== '');
            
            // Add RAI branding to greetings (bot only)
            if (sender === 'bot' && (content.toLowerCase().includes('hello') || content.toLowerCase().includes('hi there'))) {
                if (!content.includes('RAI')) {
                    content = content.replace(/^(Hello|Hi there)/i, '$1! I\'m RAI');
                }
            }
            
            // Format content with HTML
            if (paragraphs.length > 0) {
                paragraphs.forEach(paragraph => {
                    const p = document.createElement('p');
                    p.textContent = paragraph;
                    messageContent.appendChild(p);
                });
            } else {
                const p = document.createElement('p');
                p.textContent = content;
                messageContent.appendChild(p);
            }
        } else {
            // User messages remain as plain text
            messageContent.textContent = content;
        }
        
        messageDiv.appendChild(messageContent);
        chatMessages.appendChild(messageDiv);
        
        // Scroll to bottom
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function showTypingIndicator() {
        const typingDiv = document.createElement('div');
        typingDiv.classList.add('message', 'bot-message', 'typing-indicator');
        typingDiv.id = 'typingIndicator';
        
        for (let i = 0; i < 3; i++) {
            const dot = document.createElement('span');
            typingDiv.appendChild(dot);
        }
        
        chatMessages.appendChild(typingDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function removeTypingIndicator() {
        const typingIndicator = document.getElementById('typingIndicator');
        if (typingIndicator) {
            typingIndicator.remove();
        }
    }

    // Call the backend API
    async function callChatAPI(message) {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ message })
        });
        
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        
        const data = await response.json();
        return data.response;
    }
});
