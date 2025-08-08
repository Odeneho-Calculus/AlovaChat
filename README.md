# AlovaChat - AI-Powered Chat Application

AlovaChat is a modern, real-time chat application built with ASP.NET Core, Blazor Server, and SignalR. It features multiple AI service integrations including Wikipedia search, HuggingFace models, and Kaggle AI services.

## 🚀 Features

- **Real-time Chat**: Built with SignalR for instant messaging
- **Multiple AI Backends**: Support for Wikipedia Search, HuggingFace, and Kaggle AI services
- **Session Management**: Persistent chat sessions with message history
- **Responsive Design**: Modern UI with Bootstrap and custom CSS
- **Professional Architecture**: Clean separation of concerns with dependency injection
- **Error Handling**: Comprehensive logging and error recovery
- **TypeScript/JavaScript Integration**: Professional client-side chat functionality

## 🏗️ Architecture

### Technology Stack

- **Backend**: ASP.NET Core 9.0, Blazor Server
- **Real-time Communication**: SignalR
- **Frontend**: Blazor Components, Bootstrap 5, Custom CSS
- **Client-side**: JavaScript/TypeScript with SignalR client
- **Data Storage**: In-memory storage (easily extensible to databases)
- **AI Services**: Multiple provider support

### Project Structure

```
AlovaChat/
├── Components/                 # Blazor components
│   ├── Pages/                 # Page components
│   │   └── Home.razor         # Main chat interface
│   ├── MainLayout.razor       # Application layout
│   └── NavMenu.razor          # Navigation menu
├── Controllers/               # API controllers
│   └── TestController.cs      # Testing endpoints
├── Hubs/                      # SignalR hubs
│   └── ChatHub.cs            # Main chat hub
├── Models/                    # Data models
│   ├── AIResponse.cs         # AI service models
│   ├── ChatMessage.cs        # Chat message models
│   ├── ChatSession.cs        # Session models
│   └── HuggingFaceModels.cs  # HuggingFace specific models
├── Services/                  # Business logic services
│   ├── AIModelService.cs     # HuggingFace AI service
│   ├── ChatSessionService.cs # Session management
│   ├── KaggleAIModelService.cs # Kaggle AI service
│   ├── WikipediaSearchAIService.cs # Wikipedia search service
│   └── Interfaces/           # Service interfaces
├── Pages/                     # Razor pages
│   ├── Shared/               # Shared layouts
│   └── _Host.cshtml          # Main host page
├── wwwroot/                   # Static files
│   ├── css/                  # Stylesheets
│   ├── js/                   # JavaScript files
│   └── models/               # Client-side models
├── DialoGPT-medium/          # Local AI model files
├── Program.cs                # Application entry point
├── appsettings.json          # Configuration
└── AlovaChat.csproj          # Project file
```

## 🛠️ Setup and Installation

### Prerequisites

- .NET 9.0 SDK or later
- Visual Studio 2022 or VS Code
- Git

### Installation Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/Odeneho-Calculus/AlovaChat.git
   cd AlovaChat
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore AlovaChat.csproj
   ```

3. **Configure AI Services (Optional)**

   Create a `.env` file or update `appsettings.json`:
   ```json
   {
     "HuggingFace": {
       "ApiKey": "your-huggingface-api-key",
       "ModelId": "HuggingFaceH4/zephyr-7b-beta"
     },
     "Kaggle": {
       "ApiUrl": "your-kaggle-api-url"
     }
   }
   ```

4. **Build the application**
   ```bash
   dotnet build AlovaChat.csproj
   ```

5. **Run the application**
   ```bash
   dotnet run --project AlovaChat.csproj
   ```

6. **Access the application**
   - Open your browser and navigate to `http://localhost:5000`
   - The application will automatically initialize the AI service

## 🔧 Configuration

### AI Service Selection

The application supports multiple AI backends. Configure in `Program.cs`:

```csharp
// Wikipedia Search (Default - No API key required)
builder.Services.AddSingleton<IAIModelService, WikipediaSearchAIService>();

// HuggingFace AI Service
// builder.Services.AddSingleton<IAIModelService, AIModelService>();

// Kaggle AI Service
// builder.Services.AddSingleton<IAIModelService, KaggleAIModelService>();
```

### Application Settings

Key configuration options in `appsettings.json`:

```json
{
  "Wikipedia": {
    "MaxResults": 3,
    "TimeoutSeconds": 30,
    "EnableCaching": true,
    "Language": "en"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "UI": {
    "DefaultTheme": "Light",
    "FontSize": "Medium",
    "MaxChatHistory": 100
  }
}
```

## 🏛️ Service Architecture

### AI Service Interface

All AI services implement the `IAIModelService` interface:

```csharp
public interface IAIModelService
{
    Task InitializeAsync();
    Task<AIResponse> GenerateResponseAsync(AIRequest request);
    bool IsModelLoaded { get; }
    string ModelStatus { get; }
}
```

### Available AI Services

1. **WikipediaSearchAIService** (Default)
   - No API key required
   - Real Wikipedia content
   - Advanced query processing
   - Fallback search strategies
   - Rich HTML formatting

2. **AIModelService** (HuggingFace)
   - Requires HuggingFace API key
   - Multiple model support
   - Configurable parameters
   - Retry logic and error handling

3. **KaggleAIModelService**
   - Kaggle API integration
   - Custom model endpoints
   - Flexible configuration

### Session Management

The `ChatSessionService` provides:
- In-memory session storage
- Message persistence
- User session management
- Session cleanup and maintenance

## 🎨 Frontend Architecture

### Blazor Components

- **Home.razor**: Main chat interface with real-time messaging
- **MainLayout.razor**: Application shell with sidebar navigation
- **NavMenu.razor**: Session management and navigation

### JavaScript Integration

The `chat.js` file provides:
- SignalR connection management
- Real-time message handling
- UI state management
- Error handling and reconnection
- Wikipedia modal functionality

### CSS Architecture

Professional styling with:
- CSS custom properties for theming
- Responsive design patterns
- Component-specific styles
- Wikipedia search result styling
- Loading states and animations

## 🔄 Real-time Communication

### SignalR Hub Methods

The `ChatHub` provides these methods:

- `JoinSession(sessionId)`: Join a chat session
- `LeaveSession(sessionId)`: Leave a chat session
- `SendMessage(sessionId, message)`: Send a message
- `GetModelStatus()`: Get AI model status

### Client Events

- `ReceiveMessage`: New message received
- `TypingIndicator`: Show/hide typing indicator
- `ModelStatus`: AI model status updates
- `Error`: Error notifications

## 🧪 Testing

### API Testing Endpoints

The application includes test endpoints:

- `GET /api/test/status`: Check AI service status
- `POST /api/test/chat`: Test AI chat functionality
- `GET /api/test/env`: Environment information

### Manual Testing

1. Start the application
2. Open multiple browser tabs to test real-time features
3. Test different AI services by switching configurations
4. Verify session persistence and message history

## 🚀 Deployment

### Development

```bash
dotnet run --project AlovaChat.csproj
```

### Production

1. **Build for production**
   ```bash
   dotnet publish AlovaChat.csproj -c Release -o ./publish
   ```

2. **Configure environment variables**
   ```bash
   export ASPNETCORE_ENVIRONMENT=Production
   export HUGGINGFACE_API_KEY=your-api-key
   ```

3. **Run the application**
   ```bash
   dotnet ./publish/AlovaChat.dll
   ```

### Docker Deployment

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AlovaChat.csproj", "."]
RUN dotnet restore "AlovaChat.csproj"
COPY . .
RUN dotnet build "AlovaChat.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AlovaChat.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AlovaChat.dll"]
```

## 🔧 Development Guidelines

### Adding New AI Services

1. Implement the `IAIModelService` interface
2. Add service registration in `Program.cs`
3. Configure service-specific settings
4. Add appropriate error handling and logging

### Extending the UI

1. Create new Blazor components in the `Components` folder
2. Add corresponding CSS styles in `app.css`
3. Update JavaScript functionality in `chat.js` if needed
4. Follow the existing naming conventions

### Database Integration

To add database support:

1. Install Entity Framework packages
2. Create DbContext and configure models
3. Update service implementations
4. Add migration support

## 📝 API Documentation

### Chat Hub Methods

#### JoinSession
```csharp
await connection.InvokeAsync("JoinSession", sessionId);
```

#### SendMessage
```csharp
await connection.InvokeAsync("SendMessage", sessionId, message);
```

### REST API Endpoints

#### Get Status
```http
GET /api/test/status
```

Response:
```json
{
  "isModelLoaded": true,
  "modelStatus": "Ready - Wikipedia Search Engine",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

#### Test Chat
```http
POST /api/test/chat
Content-Type: application/json

{
  "message": "Hello, how are you?"
}
```

## 🐛 Troubleshooting

### Common Issues

1. **SignalR Connection Failed**
   - Check if the application is running on the correct port
   - Verify firewall settings
   - Check browser console for JavaScript errors

2. **AI Service Not Loading**
   - Verify API keys are configured correctly
   - Check network connectivity
   - Review application logs for specific errors

3. **Wikipedia Search Returning No Results**
   - The service includes fallback search strategies
   - Check the query processing logic
   - Verify Wikipedia API accessibility

### Logging

The application uses structured logging. Check logs for:
- AI service initialization
- SignalR connection events
- Message processing
- Error details

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes following the existing patterns
4. Add tests if applicable
5. Submit a pull request

### Code Style

- Follow C# naming conventions
- Use async/await patterns consistently
- Add comprehensive logging
- Include error handling
- Write clear, self-documenting code

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- Built with ASP.NET Core and Blazor
- Uses SignalR for real-time communication
- Wikipedia API for search functionality
- HuggingFace for AI model integration
- Bootstrap for responsive design

## 📞 Support

For support and questions:
- Check the troubleshooting section
- Review the application logs
- Create an issue in the repository
- Contact the development team

---

**AlovaChat** - Professional AI Chat Application with Real-time Communication