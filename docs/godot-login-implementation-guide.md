# PlayKit SDK - Godot版本登陆实现指南

## 目录
- [1. 登陆流程概览](#1-登陆流程概览)
- [2. 架构设计](#2-架构设计)
- [3. 核心组件实现](#3-核心组件实现)
- [4. API接口详解](#4-api接口详解)
- [5. 完整实现示例](#5-完整实现示例)
- [6. 最佳实践](#6-最佳实践)
- [7. 常见问题](#7-常见问题)

---

## 1. 登陆流程概览

### 1.1 完整流程图

```
[游戏启动]
    ↓
[初始化PlayKit SDK]
    ↓
[选择认证方式]
    ├─→ [开发者Token] → [直接初始化] → [完成]
    └─→ [玩家登陆 (推荐)]
            ↓
        [检查本地存储的Token]
            ├─→ [Token有效] → [验证Token] → [获取用户信息] → [完成]
            ├─→ [Token过期但有RefreshToken] → [刷新Token] → [完成]
            └─→ [无Token或已失效]
                    ↓
                [启动设备授权流程 (Device Auth Flow)]
                    ↓
                [生成PKCE安全参数]
                    ↓
                [POST /api/device-auth/initiate]
                    ↓
                [显示登陆弹窗]
                    ↓
                [玩家点击"登陆游戏"按钮]
                    ↓
                [打开系统浏览器到授权URL]
                    ↓
                [玩家在浏览器中登陆/注册]
                    ↓
                [SDK轮询授权状态]
                    ├─→ [pending] → [继续轮询]
                    ├─→ [authorized] → [获取Tokens]
                    ├─→ [denied] → [显示错误]
                    └─→ [expired] → [会话过期]
                    ↓
                [加密保存Tokens到本地]
                    ↓
                [发射'authenticated'信号]
                    ↓
                [获取玩家信息]
                    ↓
                [显示余额]
                    ↓
                [启动自动余额检查]
                    ↓
                [登陆完成]
```

### 1.2 关键特性

1. **安全性**
   - 使用OAuth 2.0 Device Authorization Grant标准
   - PKCE (Proof Key for Code Exchange) 防止授权码拦截
   - Token本地加密存储
   - 自动Token刷新机制

2. **用户体验**
   - 一键登陆，无需输入账号密码
   - 自动打开系统浏览器
   - 实时显示授权状态
   - 支持多语言（中文、英文、日文、韩文等）

3. **开发友好**
   - 支持开发者Token快速测试
   - 完整的事件系统
   - 自动处理Token过期和刷新
   - 游戏数据隔离

---

## 2. 架构设计

### 2.1 核心模块

```
PlayKitSDK (主入口)
    ├── AuthManager (认证管理器)
    │   ├── DeviceAuthFlow (设备授权流程)
    │   ├── TokenStorage (Token存储)
    │   └── TokenRefresher (Token刷新)
    │
    ├── PlayerClient (玩家客户端)
    │   ├── PlayerInfoManager (用户信息)
    │   └── BalanceChecker (余额检查)
    │
    ├── RechargeManager (充值管理)
    │   └── RechargeUI (充值界面)
    │
    └── EventEmitter (事件系统)
```

### 2.2 文件结构建议

```
addons/playkit/
├── core/
│   ├── playkit_sdk.gd              # SDK主入口
│   ├── player_client.gd            # 玩家客户端
│   └── event_emitter.gd            # 事件系统
│
├── auth/
│   ├── auth_manager.gd             # 认证管理器
│   ├── device_auth_flow.gd         # 设备授权流程
│   ├── token_storage.gd            # Token存储
│   └── pkce_generator.gd           # PKCE生成器
│
├── recharge/
│   ├── recharge_manager.gd         # 充值管理
│   └── recharge_ui.tscn            # 充值UI场景
│
├── ui/
│   ├── login_modal.tscn            # 登陆弹窗场景
│   ├── login_modal.gd              # 登陆弹窗脚本
│   └── balance_toast.tscn          # 余额提示场景
│
└── utils/
    ├── crypto_utils.gd             # 加密工具
    ├── http_client.gd              # HTTP客户端
    └── logger.gd                   # 日志工具
```

---

## 3. 核心组件实现

### 3.1 PlayKit SDK 主入口

```gdscript
# playkit_sdk.gd
extends Node
class_name PlayKitSDK

signal authenticated(auth_state: Dictionary)
signal unauthenticated()
signal token_refreshed(new_token: String)
signal balance_updated(balance: int)
signal balance_low(balance: int)
signal insufficient_credits(error: String)
signal daily_credits_refreshed(info: Dictionary)
signal error(error_msg: String)

# 配置
var config: Dictionary = {}
var game_id: String = ""
var base_url: String = "https://developerworks.cn"
var debug_mode: bool = false

# 核心模块
var auth_manager: AuthManager
var player_client: PlayerClient
var recharge_manager: RechargeManager

# 初始化SDK
func _init(init_config: Dictionary):
    config = init_config
    game_id = config.get("game_id", "")
    base_url = config.get("base_url", "https://developerworks.cn")
    debug_mode = config.get("debug", false)

    if game_id.is_empty():
        push_error("PlayKit SDK: game_id is required")
        return

    # 创建核心模块
    auth_manager = AuthManager.new(self)
    player_client = PlayerClient.new(self)
    recharge_manager = RechargeManager.new(self)

    # 连接认证事件
    auth_manager.authenticated.connect(_on_authenticated)
    auth_manager.unauthenticated.connect(_on_unauthenticated)
    auth_manager.token_refreshed.connect(_on_token_refreshed)

# 初始化
func initialize() -> void:
    await auth_manager.initialize()

    # 如果已认证，获取玩家信息
    if auth_manager.is_authenticated():
        await player_client.get_player_info()

# 手动触发登陆
func login() -> void:
    await auth_manager.start_auth_flow()

# 登出
func logout() -> void:
    await auth_manager.logout()
    unauthenticated.emit()

# 获取当前Token
func get_token() -> String:
    return auth_manager.get_token()

# 获取玩家信息
func get_player_info() -> Dictionary:
    return player_client.get_cached_player_info()

# 刷新玩家信息
func refresh_player_info() -> Dictionary:
    return await player_client.get_player_info()

# 显示充值界面
func show_recharge() -> void:
    recharge_manager.show_recharge_modal()

# 内部回调
func _on_authenticated(auth_state: Dictionary):
    authenticated.emit(auth_state)
    # 获取玩家信息
    if auth_state.token_type == "player":
        await player_client.get_player_info()

func _on_unauthenticated():
    unauthenticated.emit()

func _on_token_refreshed(new_token: String):
    token_refreshed.emit(new_token)
```

### 3.2 认证管理器 (AuthManager)

```gdscript
# auth_manager.gd
extends RefCounted
class_name AuthManager

signal authenticated(auth_state: Dictionary)
signal unauthenticated()
signal token_refreshed(new_token: String)
signal error(error_msg: String)

var sdk: PlayKitSDK
var device_auth_flow: DeviceAuthFlow
var token_storage: TokenStorage

var auth_state: Dictionary = {
    "is_authenticated": false,
    "token": "",
    "token_type": "",  # "player" or "developer"
    "expires_at": 0,
    "refresh_token": "",
    "refresh_expires_at": 0
}

func _init(playkit_sdk: PlayKitSDK):
    sdk = playkit_sdk
    token_storage = TokenStorage.new(sdk)
    device_auth_flow = DeviceAuthFlow.new(sdk)

    # 连接设备授权流程事件
    device_auth_flow.authenticated.connect(_on_device_auth_success)
    device_auth_flow.error.connect(_on_device_auth_error)

# 初始化认证
func initialize() -> void:
    await token_storage.initialize()

    # 优先级1: 开发者Token (开发模式)
    if sdk.config.has("developer_token"):
        auth_state = {
            "is_authenticated": true,
            "token": sdk.config.developer_token,
            "token_type": "developer"
        }
        authenticated.emit(auth_state)
        return

    # 优先级2: 玩家Token (服务器模式)
    if sdk.config.has("player_token"):
        auth_state = {
            "is_authenticated": true,
            "token": sdk.config.player_token,
            "token_type": "player"
        }
        authenticated.emit(auth_state)
        return

    # 优先级3: 从本地存储加载
    var saved_state = await token_storage.load_auth_state(sdk.game_id)
    if saved_state and saved_state.has("token") and not saved_state.token.is_empty():
        # 检查Token是否过期
        if saved_state.has("expires_at") and Time.get_unix_time_from_system() < saved_state.expires_at:
            auth_state = saved_state
            authenticated.emit(auth_state)
            return

        # Token过期但有RefreshToken
        if saved_state.has("refresh_token") and not saved_state.refresh_token.is_empty():
            if not saved_state.has("refresh_expires_at") or Time.get_unix_time_from_system() < saved_state.refresh_expires_at:
                auth_state = saved_state
                await refresh_token()
                return

    # 未认证
    unauthenticated.emit()

    # 自动启动登陆流程（如果配置了）
    if sdk.config.get("auto_login", false):
        await start_auth_flow()

# 启动认证流程
func start_auth_flow() -> void:
    await device_auth_flow.start_flow()

# 刷新Token
func refresh_token() -> void:
    if not can_refresh():
        push_error("PlayKit: Cannot refresh token")
        return

    var url = sdk.base_url + "/api/auth/refresh"
    var headers = ["Content-Type: application/json"]
    var body = JSON.stringify({
        "refresh_token": auth_state.refresh_token
    })

    var http = HTTPRequest.new()
    sdk.add_child(http)
    http.request_completed.connect(_on_refresh_completed)

    var err = http.request(url, headers, HTTPClient.METHOD_POST, body)
    if err != OK:
        push_error("PlayKit: Failed to send refresh request")
        error.emit("Failed to refresh token")

func _on_refresh_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    var http = get_tree().current_scene.get_node("HTTPRequest")
    http.queue_free()

    if response_code != 200:
        push_error("PlayKit: Token refresh failed with code " + str(response_code))
        # Token刷新失败，清除认证状态
        await logout()
        return

    var json = JSON.new()
    var parse_result = json.parse(body.get_string_from_utf8())
    if parse_result != OK:
        push_error("PlayKit: Failed to parse refresh response")
        return

    var data = json.data

    # 更新认证状态
    var now = Time.get_unix_time_from_system()
    auth_state.token = data.access_token
    auth_state.expires_at = now + data.expires_in
    if data.has("refresh_token"):
        auth_state.refresh_token = data.refresh_token
    if data.has("refresh_expires_in"):
        auth_state.refresh_expires_at = now + data.refresh_expires_in

    # 保存到本地
    await token_storage.save_auth_state(sdk.game_id, auth_state)

    token_refreshed.emit(auth_state.token)

# 登出
func logout() -> void:
    auth_state = {
        "is_authenticated": false,
        "token": "",
        "token_type": "",
        "expires_at": 0,
        "refresh_token": "",
        "refresh_expires_at": 0
    }
    await token_storage.clear_auth_state(sdk.game_id)
    unauthenticated.emit()

# 检查是否可以刷新
func can_refresh() -> bool:
    if auth_state.refresh_token.is_empty():
        return false
    if not auth_state.has("refresh_expires_at"):
        return true
    return Time.get_unix_time_from_system() < auth_state.refresh_expires_at

# 检查是否已认证
func is_authenticated() -> bool:
    return auth_state.is_authenticated

# 获取Token
func get_token() -> String:
    return auth_state.token

# 设备授权成功回调
func _on_device_auth_success(tokens: Dictionary):
    var now = Time.get_unix_time_from_system()
    auth_state = {
        "is_authenticated": true,
        "token": tokens.access_token,
        "token_type": "player",
        "expires_at": now + tokens.expires_in,
        "refresh_token": tokens.refresh_token,
        "refresh_expires_at": now + tokens.refresh_expires_in
    }

    # 保存到本地
    await token_storage.save_auth_state(sdk.game_id, auth_state)

    authenticated.emit(auth_state)

func _on_device_auth_error(error_msg: String):
    error.emit(error_msg)
```

### 3.3 设备授权流程 (DeviceAuthFlow)

```gdscript
# device_auth_flow.gd
extends RefCounted
class_name DeviceAuthFlow

signal authenticated(tokens: Dictionary)
signal auth_url_ready(url: String)
signal poll_status(status: String)
signal error(error_msg: String)
signal cancelled()

var sdk: PlayKitSDK
var pkce_generator: PKCEGenerator
var login_modal: Node

# 流程状态
var session_id: String = ""
var code_verifier: String = ""
var poll_interval: float = 5.0  # 秒
var is_polling: bool = false
var poll_timer: Timer

func _init(playkit_sdk: PlayKitSDK):
    sdk = playkit_sdk
    pkce_generator = PKCEGenerator.new()

# 启动授权流程
func start_flow() -> void:
    # 生成PKCE参数
    code_verifier = pkce_generator.generate_code_verifier()
    var code_challenge = await pkce_generator.generate_code_challenge(code_verifier)

    # 步骤1: 发起设备授权请求
    var url = sdk.base_url + "/api/device-auth/initiate"
    var headers = ["Content-Type: application/json"]
    var body = JSON.stringify({
        "game_id": sdk.game_id,
        "code_challenge": code_challenge,
        "code_challenge_method": "S256",
        "scope": "player:play"
    })

    var http = HTTPRequest.new()
    sdk.add_child(http)
    http.request_completed.connect(_on_initiate_completed)

    var err = http.request(url, headers, HTTPClient.METHOD_POST, body)
    if err != OK:
        error.emit("Failed to initiate device auth")

func _on_initiate_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    var http = get_tree().current_scene.get_node("HTTPRequest")
    http.queue_free()

    if response_code != 200:
        error.emit("Failed to initiate device auth: " + str(response_code))
        return

    var json = JSON.new()
    var parse_result = json.parse(body.get_string_from_utf8())
    if parse_result != OK:
        error.emit("Failed to parse initiate response")
        return

    var data = json.data
    session_id = data.session_id
    var auth_url = data.auth_url
    poll_interval = data.get("poll_interval", 5.0)

    # 发射auth_url_ready信号
    auth_url_ready.emit(auth_url)

    # 步骤2: 显示登陆弹窗
    show_login_modal(data.get("game", {}), auth_url)

# 显示登陆弹窗
func show_login_modal(game_info: Dictionary, auth_url: String) -> void:
    # 加载登陆弹窗场景
    var modal_scene = load("res://addons/playkit/ui/login_modal.tscn")
    login_modal = modal_scene.instantiate()

    # 设置游戏信息
    login_modal.set_game_info(game_info)

    # 连接按钮事件
    login_modal.login_clicked.connect(func():
        # 打开浏览器
        OS.shell_open(auth_url)
        # 开始轮询
        start_polling()
    )

    login_modal.cancelled.connect(func():
        cancel()
    )

    # 添加到场景
    sdk.get_tree().root.add_child(login_modal)

# 开始轮询
func start_polling() -> void:
    is_polling = true
    poll_timer = Timer.new()
    sdk.add_child(poll_timer)
    poll_timer.timeout.connect(_poll_for_token)
    poll_timer.start(poll_interval)

    # 立即执行第一次轮询
    _poll_for_token()

# 轮询Token
func _poll_for_token() -> void:
    if not is_polling:
        return

    var url = sdk.base_url + "/api/device-auth/poll"
    url += "?session_id=" + session_id.uri_encode()
    url += "&code_verifier=" + code_verifier.uri_encode()

    var http = HTTPRequest.new()
    sdk.add_child(http)
    http.request_completed.connect(_on_poll_completed)

    var err = http.request(url, [], HTTPClient.METHOD_GET)
    if err != OK:
        push_error("PlayKit: Failed to poll for token")

func _on_poll_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    var http = get_tree().current_scene.get_node("HTTPRequest")
    http.queue_free()

    var json = JSON.new()
    var parse_result = json.parse(body.get_string_from_utf8())
    if parse_result != OK:
        # 网络错误，继续轮询
        return

    var data = json.data

    if response_code == 200:
        if data.status == "pending":
            # 继续等待
            poll_status.emit("pending")
            # 更新轮询间隔
            if data.has("poll_interval"):
                poll_interval = data.poll_interval
                poll_timer.wait_time = poll_interval

        elif data.status == "authorized":
            # 授权成功！
            stop_polling()
            close_modal()
            poll_status.emit("authorized")

            authenticated.emit({
                "access_token": data.access_token,
                "token_type": data.token_type,
                "expires_in": data.expires_in,
                "refresh_token": data.refresh_token,
                "refresh_expires_in": data.refresh_expires_in,
                "scope": data.scope
            })

    else:
        # 处理错误
        var error_code = data.get("error", "")

        if error_code == "slow_down":
            # 减慢轮询
            poll_interval = min(poll_interval * 2, 30.0)
            poll_timer.wait_time = poll_interval
            poll_status.emit("slow_down")

        elif error_code == "access_denied":
            stop_polling()
            close_modal()
            poll_status.emit("denied")
            error.emit("User denied authorization")

        elif error_code == "expired_token":
            stop_polling()
            close_modal()
            poll_status.emit("expired")
            error.emit("Session expired")

        else:
            stop_polling()
            close_modal()
            error.emit("Device auth failed: " + error_code)

# 停止轮询
func stop_polling() -> void:
    is_polling = false
    if poll_timer:
        poll_timer.stop()
        poll_timer.queue_free()
        poll_timer = null

# 关闭弹窗
func close_modal() -> void:
    if login_modal:
        login_modal.queue_free()
        login_modal = null

# 取消授权流程
func cancel() -> void:
    stop_polling()
    close_modal()
    cancelled.emit()
```

### 3.4 PKCE生成器

```gdscript
# pkce_generator.gd
extends RefCounted
class_name PKCEGenerator

# 生成code_verifier (随机字符串)
func generate_code_verifier() -> String:
    var length = 32
    var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~"
    var verifier = ""

    for i in range(length):
        verifier += chars[randi() % chars.length()]

    return verifier

# 生成code_challenge (SHA256哈希)
func generate_code_challenge(code_verifier: String) -> String:
    # 使用SHA256哈希
    var ctx = HashingContext.new()
    ctx.start(HashingContext.HASH_SHA256)
    ctx.update(code_verifier.to_utf8_buffer())
    var hash = ctx.finish()

    # Base64 URL编码
    return Marshalls.raw_to_base64(hash).replace("+", "-").replace("/", "_").replace("=", "")
```

### 3.5 Token存储 (带加密)

```gdscript
# token_storage.gd
extends RefCounted
class_name TokenStorage

var sdk: PlayKitSDK
var encryption_key: PackedByteArray

func _init(playkit_sdk: PlayKitSDK):
    sdk = playkit_sdk

func initialize() -> void:
    # 加载或生成加密密钥
    var key_path = "user://playkit_encryption_key"
    if FileAccess.file_exists(key_path):
        var file = FileAccess.open(key_path, FileAccess.READ)
        encryption_key = file.get_buffer(32)
        file.close()
    else:
        # 生成新的加密密钥
        encryption_key = PackedByteArray()
        for i in range(32):
            encryption_key.append(randi() % 256)

        var file = FileAccess.open(key_path, FileAccess.WRITE)
        file.store_buffer(encryption_key)
        file.close()

# 保存认证状态
func save_auth_state(game_id: String, auth_state: Dictionary) -> void:
    var state_json = JSON.stringify(auth_state)
    var encrypted = encrypt(state_json)

    var file_path = "user://playkit_" + game_id + "_auth"
    var file = FileAccess.open(file_path, FileAccess.WRITE)
    file.store_string(encrypted)
    file.close()

# 加载认证状态
func load_auth_state(game_id: String) -> Dictionary:
    var file_path = "user://playkit_" + game_id + "_auth"
    if not FileAccess.file_exists(file_path):
        return {}

    var file = FileAccess.open(file_path, FileAccess.READ)
    var encrypted = file.get_as_text()
    file.close()

    var decrypted = decrypt(encrypted)
    if decrypted.is_empty():
        return {}

    var json = JSON.new()
    var parse_result = json.parse(decrypted)
    if parse_result != OK:
        return {}

    return json.data

# 清除认证状态
func clear_auth_state(game_id: String) -> void:
    var file_path = "user://playkit_" + game_id + "_auth"
    if FileAccess.file_exists(file_path):
        DirAccess.remove_absolute(file_path)

# 简单的XOR加密 (Godot 4没有内置AES)
func encrypt(data: String) -> String:
    var bytes = data.to_utf8_buffer()
    var encrypted = PackedByteArray()

    for i in range(bytes.size()):
        encrypted.append(bytes[i] ^ encryption_key[i % encryption_key.size()])

    return Marshalls.raw_to_base64(encrypted)

# 解密
func decrypt(encrypted_data: String) -> String:
    var bytes = Marshalls.base64_to_raw(encrypted_data)
    var decrypted = PackedByteArray()

    for i in range(bytes.size()):
        decrypted.append(bytes[i] ^ encryption_key[i % encryption_key.size()])

    return decrypted.get_string_from_utf8()
```

### 3.6 玩家客户端

```gdscript
# player_client.gd
extends RefCounted
class_name PlayerClient

signal player_info_updated(info: Dictionary)
signal balance_updated(balance: int)
signal balance_low(balance: int)
signal daily_credits_refreshed(info: Dictionary)
signal error(error_msg: String)

var sdk: PlayKitSDK
var player_info: Dictionary = {}
var balance_check_timer: Timer

func _init(playkit_sdk: PlayKitSDK):
    sdk = playkit_sdk

# 获取玩家信息
func get_player_info() -> Dictionary:
    var token = sdk.auth_manager.get_token()
    if token.is_empty():
        error.emit("Not authenticated")
        return {}

    var url = sdk.base_url + "/api/external/player-info"
    var headers = [
        "Authorization: Bearer " + token
    ]

    # 如果是全局开发者Token，添加X-Game-Id头
    if sdk.game_id:
        headers.append("X-Game-Id: " + sdk.game_id)

    var http = HTTPRequest.new()
    sdk.add_child(http)
    http.request_completed.connect(_on_player_info_completed)

    var err = http.request(url, headers, HTTPClient.METHOD_GET)
    if err != OK:
        error.emit("Failed to get player info")
        return {}

    # 等待响应
    await player_info_updated
    return player_info

func _on_player_info_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    var http = get_tree().current_scene.get_node("HTTPRequest")
    http.queue_free()

    # 处理认证错误
    if response_code == 401 or response_code == 403:
        await sdk.auth_manager.logout()
        error.emit("Token validation failed. Please login again.")
        return

    if response_code != 200:
        error.emit("Failed to get player info: " + str(response_code))
        return

    var json = JSON.new()
    var parse_result = json.parse(body.get_string_from_utf8())
    if parse_result != OK:
        error.emit("Failed to parse player info")
        return

    var data = json.data
    player_info = {
        "user_id": data.userId,
        "balance": data.get("balance", 0),
        "credits": data.get("credits", 0),
        "nickname": data.get("nickname", ""),
        "daily_refresh": data.get("dailyRefresh", {})
    }

    player_info_updated.emit(player_info)

    # 检查每日积分刷新
    if data.has("dailyRefresh") and data.dailyRefresh.get("refreshed", false):
        daily_credits_refreshed.emit(data.dailyRefresh)

# 获取缓存的玩家信息
func get_cached_player_info() -> Dictionary:
    return player_info

# 启用自动余额检查
func enable_auto_balance_check(interval_seconds: float = 30.0) -> void:
    balance_check_timer = Timer.new()
    sdk.add_child(balance_check_timer)
    balance_check_timer.timeout.connect(_check_balance)
    balance_check_timer.start(interval_seconds)

func _check_balance() -> void:
    var old_balance = player_info.get("balance", 0)
    await get_player_info()
    var new_balance = player_info.get("balance", 0)

    balance_updated.emit(new_balance)

    # 低余额警告
    if new_balance < 10 and new_balance != old_balance:
        balance_low.emit(new_balance)

# 设置昵称
func set_nickname(nickname: String) -> bool:
    var token = sdk.auth_manager.get_token()
    if token.is_empty():
        error.emit("Not authenticated")
        return false

    var url = sdk.base_url + "/api/external/set-game-player-nickname"
    var headers = [
        "Authorization: Bearer " + token,
        "Content-Type: application/json"
    ]

    var body = JSON.stringify({"nickname": nickname})

    var http = HTTPRequest.new()
    sdk.add_child(http)
    http.request_completed.connect(_on_set_nickname_completed)

    var err = http.request(url, headers, HTTPClient.METHOD_POST, body)
    if err != OK:
        error.emit("Failed to set nickname")
        return false

    return true

func _on_set_nickname_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    var http = get_tree().current_scene.get_node("HTTPRequest")
    http.queue_free()

    if response_code == 200:
        # 刷新玩家信息
        await get_player_info()
```

---

## 4. API接口详解

### 4.1 设备授权初始化

**接口**: `POST /api/device-auth/initiate`

**请求头**:
```
Content-Type: application/json
```

**请求体**:
```json
{
  "game_id": "your-game-id",
  "code_challenge": "base64url-encoded-sha256-hash",
  "code_challenge_method": "S256",
  "scope": "player:play"
}
```

**响应** (200 OK):
```json
{
  "session_id": "unique-session-id",
  "auth_url": "https://developerworks.cn/auth/device?session_id=xxx&code_challenge=xxx",
  "poll_interval": 5,
  "expires_in": 600,
  "game": {
    "id": "your-game-id",
    "name": "Your Game Name",
    "icon": "https://cdn.example.com/icon.png",
    "description": "Game description"
  }
}
```

### 4.2 轮询授权状态

**接口**: `GET /api/device-auth/poll`

**查询参数**:
- `session_id`: 会话ID
- `code_verifier`: PKCE验证码

**响应** (200 OK - 待授权):
```json
{
  "status": "pending",
  "poll_interval": 5
}
```

**响应** (200 OK - 已授权):
```json
{
  "status": "authorized",
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "refresh-token-string",
  "refresh_expires_in": 2592000,
  "scope": "player:play"
}
```

**响应** (400 Bad Request - 错误):
```json
{
  "error": "slow_down|access_denied|expired_token",
  "error_description": "Error description"
}
```

### 4.3 刷新Token

**接口**: `POST /api/auth/refresh`

**请求头**:
```
Content-Type: application/json
```

**请求体**:
```json
{
  "refresh_token": "your-refresh-token"
}
```

**响应** (200 OK):
```json
{
  "access_token": "new-access-token",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "new-refresh-token",
  "refresh_expires_in": 2592000,
  "scope": "player:play"
}
```

### 4.4 获取玩家信息

**接口**: `GET /api/external/player-info`

**请求头**:
```
Authorization: Bearer {token}
X-Game-Id: {game_id}  (可选，全局开发者Token时需要)
```

**响应** (200 OK):
```json
{
  "userId": "user-uuid",
  "balance": 100,
  "nickname": "PlayerName",
  "dailyRefresh": {
    "refreshed": true,
    "message": "每日积分已到账",
    "balanceBefore": 50,
    "balanceAfter": 100,
    "amountAdded": 50
  }
}
```

### 4.5 设置昵称

**接口**: `POST /api/external/set-game-player-nickname`

**请求头**:
```
Authorization: Bearer {token}
Content-Type: application/json
```

**请求体**:
```json
{
  "nickname": "NewNickname"
}
```

**响应** (200 OK):
```json
{
  "success": true,
  "nickname": "NewNickname",
  "gameId": "your-game-id"
}
```

---

## 5. 完整实现示例

### 5.1 游戏主场景使用示例

```gdscript
# game_main.gd
extends Node2D

var sdk: PlayKitSDK

func _ready():
    # 初始化PlayKit SDK
    sdk = PlayKitSDK.new({
        "game_id": "your-game-id-here",
        "debug": true,
        "auto_login": false  # 手动控制登陆时机
    })
    add_child(sdk)

    # 连接事件
    sdk.authenticated.connect(_on_authenticated)
    sdk.unauthenticated.connect(_on_unauthenticated)
    sdk.balance_updated.connect(_on_balance_updated)
    sdk.balance_low.connect(_on_balance_low)
    sdk.daily_credits_refreshed.connect(_on_daily_credits)

    # 初始化
    await sdk.initialize()

func _on_authenticated(auth_state: Dictionary):
    print("✅ 认证成功！Token类型: ", auth_state.token_type)

    # 获取玩家信息
    var player_info = sdk.get_player_info()
    print("玩家ID: ", player_info.user_id)
    print("余额: ", player_info.balance)
    print("昵称: ", player_info.nickname)

    # 启用自动余额检查
    sdk.player_client.enable_auto_balance_check(30.0)

    # 显示游戏主界面
    show_game_ui()

func _on_unauthenticated():
    print("❌ 未认证")
    show_login_button()

func _on_balance_updated(balance: int):
    print("💰 余额更新: ", balance)
    update_balance_display(balance)

func _on_balance_low(balance: int):
    print("⚠️ 余额不足: ", balance)
    show_recharge_prompt()

func _on_daily_credits(info: Dictionary):
    print("🎁 每日积分已到账: +" + str(info.amountAdded))
    show_daily_reward_toast(info)

# UI相关函数
func show_login_button():
    # 显示登陆按钮
    $LoginButton.visible = true

func _on_login_button_pressed():
    await sdk.login()

func show_game_ui():
    $LoginButton.visible = false
    $GameUI.visible = true

func update_balance_display(balance: int):
    $GameUI/BalanceLabel.text = "积分: " + str(balance)

func show_recharge_prompt():
    sdk.show_recharge()
```

### 5.2 登陆弹窗UI示例

```gdscript
# login_modal.gd
extends Control

signal login_clicked()
signal cancelled()

@onready var game_name_label = $Panel/VBox/GameName
@onready var game_icon = $Panel/VBox/GameIcon
@onready var login_button = $Panel/VBox/LoginButton
@onready var cancel_button = $Panel/VBox/CancelButton
@onready var status_label = $Panel/VBox/StatusLabel

var translations = {
    "en": {
        "title": "Login to Play",
        "button": "Login with PlayKit",
        "subtitle": "uses PlayKit for secure login",
        "waiting": "Waiting for authorization..."
    },
    "zh": {
        "title": "登录游戏",
        "button": "使用 PlayKit 登录",
        "subtitle": "使用 PlayKit 安全登录",
        "waiting": "等待授权中..."
    }
}

func _ready():
    # 检测语言
    var locale = OS.get_locale().substr(0, 2)
    var lang = "zh" if locale in ["zh", "ja", "ko"] else "en"

    # 设置文本
    login_button.text = translations[lang].button

    # 连接信号
    login_button.pressed.connect(_on_login_pressed)
    cancel_button.pressed.connect(_on_cancel_pressed)

func set_game_info(game_info: Dictionary):
    game_name_label.text = game_info.get("name", "Game")

    # 加载游戏图标
    if game_info.has("icon") and game_info.icon:
        var http = HTTPRequest.new()
        add_child(http)
        http.request_completed.connect(_on_icon_loaded)
        http.request(game_info.icon)

func _on_icon_loaded(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    if response_code == 200:
        var image = Image.new()
        var error = image.load_png_from_buffer(body)
        if error == OK:
            game_icon.texture = ImageTexture.create_from_image(image)

func _on_login_pressed():
    login_clicked.emit()

    # 更新UI状态
    login_button.disabled = true
    status_label.text = "正在打开浏览器..."

    # 1秒后显示等待消息
    await get_tree().create_timer(1.0).timeout
    status_label.text = "等待授权中..."

func _on_cancel_pressed():
    cancelled.emit()
    queue_free()
```

---

## 6. 最佳实践

### 6.1 安全性

1. **永远不要在客户端硬编码Token**
   ```gdscript
   # ❌ 错误做法
   var sdk = PlayKitSDK.new({
       "game_id": "my-game",
       "developer_token": "hardcoded-token-12345"  # 危险！
   })

   # ✅ 正确做法
   var sdk = PlayKitSDK.new({
       "game_id": "my-game"
       # 让SDK自动处理玩家认证
   })
   ```

2. **加密存储Token**
   - 使用TokenStorage类的加密功能
   - 不要明文保存到配置文件

3. **自动处理Token过期**
   ```gdscript
   # SDK会自动刷新Token，你只需要处理失败情况
   sdk.error.connect(func(error_msg):
       if "Token validation failed" in error_msg:
           # 提示用户重新登陆
           show_login_prompt()
   )
   ```

### 6.2 用户体验

1. **静默登陆**
   ```gdscript
   # 游戏启动时自动检查登陆状态
   func _ready():
       sdk = PlayKitSDK.new({"game_id": "my-game"})
       add_child(sdk)

       await sdk.initialize()

       # 如果已登陆，直接进入游戏
       if sdk.auth_manager.is_authenticated():
           start_game()
       else:
           show_login_screen()
   ```

2. **友好的错误提示**
   ```gdscript
   sdk.error.connect(func(error_msg):
       match error_msg:
           "User denied authorization":
               show_message("登陆已取消")
           "Session expired":
               show_message("登陆超时，请重试")
           _:
               show_message("登陆失败: " + error_msg)
   )
   ```

3. **实时余额显示**
   ```gdscript
   # 启用自动余额检查
   sdk.player_client.enable_auto_balance_check(30.0)

   # 监听余额变化
   sdk.balance_updated.connect(func(balance):
       $UI/BalanceLabel.text = str(balance)
   )
   ```

### 6.3 性能优化

1. **缓存玩家信息**
   ```gdscript
   # 使用缓存避免重复请求
   var player_info = sdk.get_player_info()  # 使用缓存

   # 需要最新数据时才刷新
   if need_fresh_data:
       player_info = await sdk.refresh_player_info()
   ```

2. **合理的轮询间隔**
   ```gdscript
   # 余额检查不需要太频繁
   sdk.player_client.enable_auto_balance_check(60.0)  # 60秒
   ```

3. **释放不用的资源**
   ```gdscript
   func _exit_tree():
       # 游戏退出时清理
       if sdk.player_client.balance_check_timer:
           sdk.player_client.balance_check_timer.stop()
   ```

---

## 7. 常见问题

### Q1: 如何在开发时快速测试？

**A**: 使用开发者Token模式，跳过登陆流程

```gdscript
var sdk = PlayKitSDK.new({
    "game_id": "my-game",
    "developer_token": "your-dev-token-from-dashboard",
    "debug": true
})
```

### Q2: 如何处理Token过期？

**A**: SDK会自动刷新Token。如果刷新失败，会触发`unauthenticated`信号

```gdscript
sdk.unauthenticated.connect(func():
    # Token无法刷新，需要重新登陆
    show_login_screen()
)
```

### Q3: 如何在无头模式（服务器）使用？

**A**: 提供playerToken或developerToken

```gdscript
var sdk = PlayKitSDK.new({
    "game_id": "my-game",
    "player_token": "player-token-from-server",
    "mode": "server"
})
```

### Q4: 登陆弹窗被浏览器拦截怎么办？

**A**: SDK已经处理了这个问题。弹窗在用户点击按钮后打开，不会被拦截。

### Q5: 如何支持多个游戏？

**A**: 每个游戏使用不同的game_id，Token会自动隔离

```gdscript
# 游戏A
var sdk_a = PlayKitSDK.new({"game_id": "game-a"})

# 游戏B
var sdk_b = PlayKitSDK.new({"game_id": "game-b"})
```

### Q6: 如何测试充值流程？

**A**: 使用测试环境和测试Token

```gdscript
var sdk = PlayKitSDK.new({
    "game_id": "my-game",
    "base_url": "https://test.developerworks.cn",  # 测试环境
    "debug": true
})
```

### Q7: 玩家余额不足如何提示充值？

**A**: 监听`balance_low`或`insufficient_credits`事件

```gdscript
sdk.balance_low.connect(func(balance):
    show_recharge_prompt()
)

sdk.insufficient_credits.connect(func(error):
    # 自动显示充值界面
    sdk.show_recharge()
)
```

---

## 8. 进阶功能

### 8.1 多语言支持

```gdscript
# 在登陆弹窗中自动检测系统语言
func detect_language() -> String:
    var locale = OS.get_locale()
    if locale.begins_with("zh"):
        return "zh"
    elif locale.begins_with("ja"):
        return "ja"
    elif locale.begins_with("ko"):
        return "ko"
    else:
        return "en"
```

### 8.2 离线模式处理

```gdscript
func check_network() -> bool:
    # 检查网络连接
    var test_url = sdk.base_url + "/health"
    var http = HTTPRequest.new()
    add_child(http)

    var response = await http.request_completed
    http.queue_free()

    return response[1] == 200

# 使用
if not await check_network():
    show_message("网络连接失败，请检查网络")
```

### 8.3 自动重连

```gdscript
var reconnect_attempts = 0
var max_reconnect_attempts = 3

sdk.error.connect(func(error_msg):
    if "network" in error_msg.to_lower():
        if reconnect_attempts < max_reconnect_attempts:
            reconnect_attempts += 1
            await get_tree().create_timer(2.0).timeout
            await sdk.initialize()
        else:
            show_message("连接失败，请检查网络")
)
```

---

## 附录

### A. 完整的事件列表

| 事件名 | 参数 | 说明 |
|--------|------|------|
| `authenticated` | `auth_state: Dictionary` | 认证成功 |
| `unauthenticated` | 无 | 未认证或登出 |
| `token_refreshed` | `new_token: String` | Token已刷新 |
| `balance_updated` | `balance: int` | 余额已更新 |
| `balance_low` | `balance: int` | 余额不足(<10) |
| `insufficient_credits` | `error: String` | 积分不足 |
| `daily_credits_refreshed` | `info: Dictionary` | 每日积分到账 |
| `player_info_updated` | `info: Dictionary` | 玩家信息已更新 |
| `error` | `error_msg: String` | 错误发生 |
| `auth_url_ready` | `url: String` | 授权URL就绪 |
| `poll_status` | `status: String` | 轮询状态变化 |
| `cancelled` | 无 | 用户取消登陆 |

### B. 认证状态字段

```gdscript
{
    "is_authenticated": bool,
    "token": String,
    "token_type": String,  # "player" or "developer"
    "expires_at": int,     # Unix timestamp
    "refresh_token": String,
    "refresh_expires_at": int  # Unix timestamp
}
```

### C. 玩家信息字段

```gdscript
{
    "user_id": String,
    "balance": int,
    "nickname": String,
    "daily_refresh": {
        "refreshed": bool,
        "message": String,
        "balanceBefore": int,
        "balanceAfter": int,
        "amountAdded": int
    }
}
```

---

## 总结

本指南提供了PlayKit SDK在Godot中实现玩家鉴权登陆的完整方案。关键要点：

1. 使用**设备授权流程 (Device Auth Flow)** - 安全、简单、用户友好
2. 实现**PKCE安全机制** - 防止授权码拦截攻击
3. **加密存储Token** - 保护用户数据安全
4. **自动Token刷新** - 提供无缝的用户体验
5. **完善的事件系统** - 方便游戏响应各种状态变化

参考JavaScript SDK的实现，遵循本指南的最佳实践，你可以快速在Godot游戏中集成PlayKit的登陆系统。

如有问题，请参考：
- JavaScript SDK源码: `D:\Project\DeveloperWorks-JavascriptFE`
- API文档: https://developerworks.cn/docs
- 开发者控制台: https://developerworks.cn/dashboard
