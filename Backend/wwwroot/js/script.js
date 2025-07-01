document.addEventListener('DOMContentLoaded', () => {
    const chatMessages = document.getElementById('chatMessages');
    const userInput = document.getElementById('userInput');
    const sendButton = document.getElementById('sendButton');
    const clearChatButton = document.querySelector('.action-button[title="Clear Chat"]');
    const settingsButton = document.querySelector('.action-button[title="Settings"]');

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
        addMessage(message, 'user');

        // Clear input and reset height
        userInput.value = '';
        userInput.style.height = 'auto';

        // Show typing indicator
        showTypingIndicator();

        try {
            // Call the backend API
            const response = await callChatAPI(message);
            
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
        }
    }

    function addMessage(content, sender) {
        const messageDiv = document.createElement('div');
        messageDiv.classList.add('message', `${sender}-message`);
        
        const messageContent = document.createElement('div');
        messageContent.classList.add('message-content');
        
        // Format bot messages with better styling
        if (sender === 'bot') {
            // Convert line breaks to paragraphs
            const paragraphs = content.split('\n').filter(p => p.trim() !== '');
            
            // Add RAI branding to greetings
            if (content.toLowerCase().includes('hello') || content.toLowerCase().includes('hi there')) {
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
