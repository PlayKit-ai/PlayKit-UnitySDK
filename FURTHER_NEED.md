# PlayKit SDK - Future API Requirements

This document outlines the backend API endpoints needed to fully support the PlayKit Unity SDK Editor tools.

## Overview

The Unity SDK Editor window (`Tools > PlayKit SDK > Settings`) includes several features that require server-side API support. These features are currently disabled or show placeholder functionality until the corresponding API endpoints are implemented.

## Required API Endpoints

### 1. Model Discovery APIs

These endpoints allow the Unity Editor to query available AI models dynamically, enabling developers to select models from a dropdown instead of manually typing model names.

#### 1.1 Get Available Chat Models

**Endpoint:** `GET /api/v1/models/chat`

**Description:** Returns a list of available chat/text generation models for the specified game.

**Authentication:** Game ID + API Key (or Developer Token)

**Request Headers:**
```
Authorization: Bearer <developer_token>
X-Game-ID: <game_id>
```

**Response (200 OK):**
```json
{
  "success": true,
  "models": [
    {
      "id": "gpt-4o",
      "name": "GPT-4 Optimized",
      "description": "Latest GPT-4 model optimized for speed and cost",
      "provider": "openai",
      "context_window": 128000,
      "supports_streaming": true,
      "cost_per_1k_tokens": {
        "input": 0.005,
        "output": 0.015
      }
    },
    {
      "id": "gpt-4o-mini",
      "name": "GPT-4 Mini",
      "description": "Faster, more affordable GPT-4 variant",
      "provider": "openai",
      "context_window": 128000,
      "supports_streaming": true,
      "cost_per_1k_tokens": {
        "input": 0.00015,
        "output": 0.0006
      }
    },
    {
      "id": "claude-3-5-sonnet-20241022",
      "name": "Claude 3.5 Sonnet",
      "description": "Anthropic's most intelligent model",
      "provider": "anthropic",
      "context_window": 200000,
      "supports_streaming": true,
      "cost_per_1k_tokens": {
        "input": 0.003,
        "output": 0.015
      }
    }
  ],
  "default_model": "gpt-4o-mini"
}
```

**Error Response (401 Unauthorized):**
```json
{
  "success": false,
  "error": "Invalid authentication credentials"
}
```

**Error Response (403 Forbidden):**
```json
{
  "success": false,
  "error": "Game ID not authorized for this account"
}
```

---

#### 1.2 Get Available Image Models

**Endpoint:** `GET /api/v1/models/image`

**Description:** Returns a list of available image generation models.

**Authentication:** Game ID + API Key (or Developer Token)

**Request Headers:**
```
Authorization: Bearer <developer_token>
X-Game-ID: <game_id>
```

**Response (200 OK):**
```json
{
  "success": true,
  "models": [
    {
      "id": "dall-e-3",
      "name": "DALL-E 3",
      "description": "OpenAI's latest image generation model",
      "provider": "openai",
      "supported_sizes": ["1024x1024", "1024x1792", "1792x1024"],
      "max_images_per_request": 1,
      "cost_per_image": {
        "1024x1024": 0.04,
        "1024x1792": 0.08,
        "1792x1024": 0.08
      }
    },
    {
      "id": "dall-e-2",
      "name": "DALL-E 2",
      "description": "Previous generation, more affordable",
      "provider": "openai",
      "supported_sizes": ["256x256", "512x512", "1024x1024"],
      "max_images_per_request": 10,
      "cost_per_image": {
        "256x256": 0.016,
        "512x512": 0.018,
        "1024x1024": 0.02
      }
    }
  ],
  "default_model": "dall-e-3"
}
```

---

### 2. Configuration Validation API

This endpoint validates the Game ID and Developer Token configuration before runtime, helping developers catch configuration errors early.

#### 2.1 Validate Configuration

**Endpoint:** `POST /api/v1/validate`

**Description:** Validates that a Game ID and optional Developer Token are correct and authorized.

**Authentication:** Game ID + Developer Token (optional)

**Request Body:**
```json
{
  "game_id": "game_abc123",
  "developer_token": "dev_xyz789_optional"
}
```

**Response (200 OK - Valid):**
```json
{
  "success": true,
  "valid": true,
  "game_info": {
    "id": "game_abc123",
    "name": "My Awesome Game",
    "status": "active"
  },
  "authentication": {
    "type": "developer_token",
    "valid": true,
    "expires_at": "2025-12-31T23:59:59Z"
  },
  "quotas": {
    "chat_requests_remaining": 5000,
    "image_requests_remaining": 500,
    "reset_at": "2025-12-01T00:00:00Z"
  }
}
```

**Response (200 OK - Invalid Game ID):**
```json
{
  "success": true,
  "valid": false,
  "error": "game_not_found",
  "message": "Game ID 'game_abc123' does not exist"
}
```

**Response (200 OK - Invalid Token):**
```json
{
  "success": true,
  "valid": false,
  "error": "invalid_token",
  "message": "Developer token is invalid or expired"
}
```

**Response (200 OK - Game Suspended):**
```json
{
  "success": true,
  "valid": false,
  "error": "game_suspended",
  "message": "This game has been suspended. Please contact support."
}
```

---

### 3. Game Information API

Provides detailed information about a game for display in the Unity Editor.

#### 3.1 Get Game Info

**Endpoint:** `GET /api/v1/games/{game_id}`

**Description:** Returns detailed information about a specific game.

**Authentication:** Game ID + Developer Token

**Request Headers:**
```
Authorization: Bearer <developer_token>
X-Game-ID: <game_id>
```

**Response (200 OK):**
```json
{
  "success": true,
  "game": {
    "id": "game_abc123",
    "name": "My Awesome Game",
    "description": "An epic adventure game",
    "status": "active",
    "created_at": "2024-01-15T10:30:00Z",
    "owner": {
      "id": "user_xyz",
      "email": "developer@example.com"
    },
    "settings": {
      "default_chat_model": "gpt-4o-mini",
      "default_image_model": "dall-e-3",
      "allowed_models": ["gpt-4o", "gpt-4o-mini", "claude-3-5-sonnet-20241022"],
      "rate_limits": {
        "requests_per_minute": 60,
        "concurrent_requests": 10
      }
    },
    "usage": {
      "current_month": {
        "chat_requests": 1523,
        "image_requests": 87,
        "total_tokens": 2547893,
        "estimated_cost_usd": 12.45
      }
    },
    "quotas": {
      "chat_requests_limit": 10000,
      "image_requests_limit": 1000,
      "reset_date": "2025-12-01T00:00:00Z"
    }
  }
}
```

---

## Authentication Methods

### Developer Token (Editor Only)

- Used in Unity Editor for testing and development
- Should be stored in `EditorPrefs` (not committed to version control)
- Format: `dev_<random_string>`
- Never used in production builds

### Player Token (Runtime)

- Used in production builds
- Obtained through player authentication flow
- Stored in `PlayerPrefs`
- Has expiration time

### API Key (Future Enhancement)

For future consideration: Allow developers to create API keys specifically for Unity Editor tools, separate from developer tokens.

---

## Security Considerations

### 1. Rate Limiting

All API endpoints should implement rate limiting:
- Per Game ID: 100 requests per minute
- Per IP: 200 requests per minute (for editor tools)

### 2. Token Validation

- Developer tokens should be validated on every request
- Expired tokens should return `401 Unauthorized`
- Include token expiration in validation responses

### 3. CORS Headers

Editor tools may make requests from Unity Editor, ensure CORS headers allow:
```
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET, POST
Access-Control-Allow-Headers: Authorization, X-Game-ID, Content-Type
```

---

## Implementation Priority

### Phase 1 (High Priority)
1. **Validate Configuration API** - Critical for error prevention
2. **Get Game Info API** - Useful for debugging and developer experience

### Phase 2 (Medium Priority)
3. **Get Available Chat Models** - Improves usability, reduces typos
4. **Get Available Image Models** - Same as above

### Phase 3 (Nice to Have)
- Enhanced usage analytics
- Model recommendation based on usage patterns
- Cost estimation tools

---

## Unity Editor Integration Notes

### How Unity Will Use These APIs

```csharp
// Example: Fetching chat models
public async Task<List<ChatModel>> FetchChatModels()
{
    string gameId = PlayKitSettings.Instance.GameId;
    string devToken = PlayKitSettings.DeveloperToken;

    using (UnityWebRequest request = UnityWebRequest.Get($"{API_BASE}/api/v1/models/chat"))
    {
        request.SetRequestHeader("Authorization", $"Bearer {devToken}");
        request.SetRequestHeader("X-Game-ID", gameId);

        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<ModelsResponse>(request.downloadHandler.text);
            return response.models;
        }
    }

    return null;
}
```

### Editor Window Usage

- **Model Dropdowns**: Populate from API when window opens
- **Connection Test**: Call validation API and display result
- **Game Info Display**: Show game name, status, usage stats in Settings window
- **Auto-refresh**: Cache model lists for 1 hour, allow manual refresh

---

## Testing Endpoints

For development and testing, mock endpoints should return:
- Success responses with sample data
- Various error conditions
- Rate limit responses
- Authentication failures

This allows Unity SDK developers to test error handling before production APIs are ready.

---

## Questions & Feedback

If you have questions about these API requirements or need clarification on any endpoint, please contact the Unity SDK team or create an issue on the SDK repository.

**Last Updated:** 2025-11-13
**Document Version:** 1.0
**SDK Version:** v0.1.7.3-beta
