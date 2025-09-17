// AlovaChat - Professional Chat Application JavaScript - Fixed Version

// Define AlovaChatClient class globally
window.AlovaChatClient = class AlovaChatClient {
    constructor() {
        this.connection = null;
        this.currentSessionId = null;
        this.dotNetRef = null;
        this.isConnected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.userId = this.getUserId();

        this.initializeConnection();
    }

    // Initialize SignalR connection
    async initializeConnection() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/chathub")
                .withAutomaticReconnect([0, 2000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            this.setupEventHandlers();
            await this.startConnection();
        } catch (error) {
            console.error('Error initializing SignalR connection:', error);
            this.updateConnectionStatus('error');
        }
    }

    // Setup SignalR event handlers
    setupEventHandlers() {
        // Connection events
        this.connection.onclose(async (error) => {
            console.log('SignalR connection closed:', error);
            this.isConnected = false;
            this.updateConnectionStatus('disconnected');
        });

        this.connection.onreconnecting((error) => {
            console.log('SignalR reconnecting:', error);
            this.updateConnectionStatus('reconnecting');
        });

        this.connection.onreconnected((connectionId) => {
            console.log('SignalR reconnected:', connectionId);
            this.isConnected = true;
            this.updateConnectionStatus('connected');

            // Rejoin current session if exists
            if (this.currentSessionId) {
                this.joinSession(this.currentSessionId);
            }
        });

        // Chat events
        this.connection.on("ReceiveMessage", (messageData) => {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnMessageReceived', messageData);
            }
        });

        this.connection.on("TypingIndicator", (isTyping) => {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnTypingIndicator', isTyping);
            }
        });

        this.connection.on("Error", (error) => {
            console.error('Chat error:', error);
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnError', error);
            }
        });

        this.connection.on("ModelStatus", (status) => {
            this.updateModelStatus(status);
        });
    }

    // Start SignalR connection
    async startConnection() {
        try {
            await this.connection.start();
            console.log('SignalR connection established');
            this.isConnected = true;
            this.reconnectAttempts = 0;
            this.updateConnectionStatus('connected');

            // Request model status
            await this.connection.invoke("GetModelStatus");
        } catch (error) {
            console.error('Error starting SignalR connection:', error);
            this.isConnected = false;
            this.updateConnectionStatus('error');

            // Retry connection
            if (this.reconnectAttempts < this.maxReconnectAttempts) {
                this.reconnectAttempts++;
                setTimeout(() => this.startConnection(), 5000);
            }
        }
    }

    // Join a chat session
    async joinSession(sessionId) {
        if (!this.isConnected || !sessionId) return;

        try {
            await this.connection.invoke("JoinSession", sessionId);
            this.currentSessionId = sessionId;
            console.log('Joined session:', sessionId);
        } catch (error) {
            console.error('Error joining session:', error);
        }
    }

    // Leave a chat session
    async leaveSession(sessionId) {
        if (!this.isConnected || !sessionId) return;

        try {
            await this.connection.invoke("LeaveSession", sessionId);
            if (this.currentSessionId === sessionId) {
                this.currentSessionId = null;
            }
            console.log('Left session:', sessionId);
        } catch (error) {
            console.error('Error leaving session:', error);
        }
    }

    // Send a message
    async sendMessage(sessionId, message) {
        if (!this.isConnected || !sessionId || !message.trim()) return;

        try {
            await this.connection.invoke("SendMessage", sessionId, message.trim());
        } catch (error) {
            console.error('Error sending message:', error);
            throw error;
        }
    }

    // Update connection status indicator
    updateConnectionStatus(status) {
        const indicator = document.getElementById('status-indicator');
        const statusText = document.getElementById('status-text');

        if (indicator && statusText) {
            indicator.className = 'status-indicator';

            switch (status) {
                case 'connected':
                    indicator.classList.add('ready');
                    statusText.textContent = 'Connected';
                    break;
                case 'reconnecting':
                    indicator.classList.add('loading');
                    statusText.textContent = 'Reconnecting...';
                    break;
                case 'disconnected':
                    indicator.classList.add('error');
                    statusText.textContent = 'Disconnected';
                    break;
                case 'error':
                    indicator.classList.add('error');
                    statusText.textContent = 'Connection Error';
                    break;
                default:
                    statusText.textContent = 'Unknown';
            }
        }
    }

    // Update model status
    updateModelStatus(status) {
        const indicator = document.getElementById('status-indicator');
        const statusText = document.getElementById('status-text');

        if (indicator && statusText) {
            indicator.className = 'status-indicator';

            if (status.IsLoaded) {
                indicator.classList.add('ready');
                statusText.textContent = 'AI Ready';
            } else {
                indicator.classList.add('loading');
                statusText.textContent = status.Status || 'Loading AI...';
            }
        }
    }

    // Get or create user ID
    getUserId() {
        let userId = localStorage.getItem('alovachat_user_id');
        if (!userId) {
            userId = this.generateUUID();
            localStorage.setItem('alovachat_user_id', userId);
        }
        return userId;
    }

    // Generate UUID
    generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c == 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    // Set .NET reference for callbacks
    setDotNetReference(dotNetRef) {
        this.dotNetRef = dotNetRef;
    }
};

// Global chat client instance
let chatClient = null;

// Initialize chat client when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    if (!chatClient) {
        chatClient = new window.AlovaChatClient();
        console.log('AlovaChatClient initialized successfully');
    }
});

// Global functions for Blazor interop
window.initializeChatHub = function (dotNetRef) {
    if (chatClient) {
        chatClient.setDotNetReference(dotNetRef);
    }
};

window.joinChatSession = async function (sessionId) {
    if (chatClient) {
        await chatClient.joinSession(sessionId);
    }
};

window.leaveChatSession = async function (sessionId) {
    if (chatClient) {
        await chatClient.leaveSession(sessionId);
    }
};

window.sendChatMessage = async function (sessionId, message) {
    if (chatClient) {
        await chatClient.sendMessage(sessionId, message);
    }
};

window.getUserId = function () {
    return chatClient ? chatClient.userId : null;
};

window.getSessionFromUrl = function () {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get('session') || '';
};

window.updateUrl = function (sessionId) {
    const url = new URL(window.location);
    if (sessionId) {
        url.searchParams.set('session', sessionId);
    } else {
        url.searchParams.delete('session');
    }
    window.history.replaceState({}, '', url);
};

window.scrollToBottom = function () {
    const chatMessages = document.getElementById('chat-messages');
    if (chatMessages) {
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }
};

window.initializeModelStatus = async function () {
    // Request initial model status
    if (chatClient && chatClient.isConnected) {
        try {
            await chatClient.connection.invoke("GetModelStatus");
        } catch (error) {
            console.error('Error getting model status:', error);
        }
    }
};

// Auto-scroll to bottom when new messages arrive
window.autoScrollToBottom = function () {
    const chatMessages = document.getElementById('chat-messages');
    if (chatMessages) {
        const isScrolledToBottom = chatMessages.scrollHeight - chatMessages.clientHeight <= chatMessages.scrollTop + 1;
        if (isScrolledToBottom) {
            setTimeout(() => {
                chatMessages.scrollTop = chatMessages.scrollHeight;
            }, 100);
        }
    }
};

// Handle keyboard shortcuts
document.addEventListener('keydown', function (event) {
    // Ctrl/Cmd + Enter to send message
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
        const sendButton = document.querySelector('.send-button');
        if (sendButton && !sendButton.disabled) {
            sendButton.click();
        }
    }

    // Escape to clear input
    if (event.key === 'Escape') {
        const chatInput = document.querySelector('.chat-input');
        if (chatInput && document.activeElement === chatInput) {
            chatInput.value = '';
            chatInput.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }
});

// Handle window focus/blur for better UX
window.addEventListener('focus', function () {
    if (chatClient && !chatClient.isConnected) {
        chatClient.startConnection();
    }
});

// Handle online/offline status
window.addEventListener('online', function () {
    console.log('Browser is online');
    if (chatClient && !chatClient.isConnected) {
        chatClient.startConnection();
    }
});

window.addEventListener('offline', function () {
    console.log('Browser is offline');
    chatClient?.updateConnectionStatus('disconnected');
});

// Utility functions
window.formatFileSize = function (bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

window.copyToClipboard = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (error) {
        console.error('Failed to copy to clipboard:', error);
        return false;
    }
};

// Wikipedia modal functionality
window.openWikipediaModal = function (url, title) {
    try {
        // Open Wikipedia article in a new tab/window
        const newWindow = window.open(url, '_blank', 'noopener,noreferrer');

        if (!newWindow) {
            // Fallback if popup was blocked
            console.warn('Popup blocked, redirecting in current tab');
            window.location.href = url;
        } else {
            // Focus the new window
            newWindow.focus();
        }
    } catch (error) {
        console.error('Error opening Wikipedia modal:', error);
        // Fallback to direct navigation
        window.location.href = url;
    }
};

// Performance monitoring
window.measurePerformance = function (name, fn) {
    const start = performance.now();
    const result = fn();
    const end = performance.now();
    console.log(`${name} took ${end - start} milliseconds`);
    return result;
};

// Error handling
window.addEventListener('error', function (event) {
    console.error('Global error:', event.error);
});

window.addEventListener('unhandledrejection', function (event) {
    console.error('Unhandled promise rejection:', event.reason);
});

// Cleanup on page unload
window.addEventListener('beforeunload', function () {
    if (chatClient && chatClient.connection) {
        chatClient.connection.stop();
    }
});