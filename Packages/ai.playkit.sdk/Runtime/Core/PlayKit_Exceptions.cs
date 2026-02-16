using System;

namespace PlayKit_SDK
{
    /// <summary>
    /// Base exception class for all PlayKit SDK exceptions
    /// </summary>
    public class PlayKitException : Exception
    {
        public string ErrorCode { get; }
        public int? HttpStatusCode { get; }

        public PlayKitException(string message, string errorCode = null, int? httpStatusCode = null)
            : base(message)
        {
            ErrorCode = errorCode;
            HttpStatusCode = httpStatusCode;
        }

        public PlayKitException(string message, Exception innerException, string errorCode = null, int? httpStatusCode = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            HttpStatusCode = httpStatusCode;
        }
    }

    /// <summary>
    /// Exception thrown when image size validation fails
    /// </summary>
    public class PlayKitImageSizeValidationException : PlayKitException
    {
        public string ProvidedSize { get; }
        public string ValidationMode { get; }

        public PlayKitImageSizeValidationException(string message, string errorCode, string providedSize = null, string validationMode = null)
            : base(message, errorCode, 400)
        {
            ProvidedSize = providedSize;
            ValidationMode = validationMode;
        }
    }
    /// <summary>
    /// Exception thrown when the API returns an error response
    /// </summary>
    public class PlayKitApiErrorException : PlayKitException
    {
        public PlayKitApiErrorException(string message, string errorCode, int httpStatusCode)
            : base(message, errorCode, httpStatusCode)
        {
        }
    }

    /// <summary>
    /// Error codes from the API
    /// </summary>
    public static class PlayKit_ErrorCodes
    {
        // Authentication errors
        public const string INVALID_TOKEN = "INVALID_TOKEN";
        public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
        public const string UNAUTHORIZED = "UNAUTHORIZED";
        public const string AUTH_MISSING_HEADER = "AUTH_MISSING_HEADER";
        public const string AUTH_INVALID_TOKEN = "AUTH_INVALID_TOKEN";
        public const string AUTH_TOKEN_GAME_MISMATCH = "AUTH_TOKEN_GAME_MISMATCH";

        // Game errors
        public const string GAME_NOT_FOUND = "GAME_NOT_FOUND";
        public const string GAME_ACCESS_DENIED = "GAME_ACCESS_DENIED";
        public const string GAME_SUSPENDED = "GAME_SUSPENDED";

        // Endpoint errors
        public const string ENDPOINT_NOT_FOUND = "ENDPOINT_NOT_FOUND";
        public const string ENDPOINT_SUSPENDED = "ENDPOINT_SUSPENDED";
        public const string MODEL_NOT_FOUND = "MODEL_NOT_FOUND";
        public const string INVALID_ENDPOINT = "INVALID_ENDPOINT";

        // Provider errors
        public const string PROVIDER_ERROR = "PROVIDER_ERROR";
        public const string PROVIDER_RATE_LIMIT = "PROVIDER_RATE_LIMIT";
        public const string PROVIDER_PAYMENT_REQUIRED = "PROVIDER_PAYMENT_REQUIRED";
        public const string PROVIDER_UNAVAILABLE = "PROVIDER_UNAVAILABLE";

        // Request errors
        public const string INVALID_REQUEST = "INVALID_REQUEST";
        public const string MISSING_PARAMETERS = "MISSING_PARAMETERS";
        public const string VALIDATION_ERROR = "VALIDATION_ERROR";
        public const string MISSING_PARAMS = "MISSING_PARAMS";

        // Credit errors
        public const string PLAYER_INSUFFICIENT_CREDIT = "PLAYER_INSUFFICIENT_CREDIT";
        public const string INSUFFICIENT_CREDITS = "INSUFFICIENT_CREDITS";
        public const string INSUFFICIENT_DEVELOPER_BALANCE = "INSUFFICIENT_DEVELOPER_BALANCE";

        // Image generation specific errors
        public const string INVALID_SIZE_FORMAT = "INVALID_SIZE_FORMAT";
        public const string INVALID_SIZE_VALUE = "INVALID_SIZE_VALUE";
        public const string SIZE_EXCEEDS_LIMIT = "SIZE_EXCEEDS_LIMIT";
        public const string SIZE_NOT_MULTIPLE = "SIZE_NOT_MULTIPLE";
        public const string SIZE_NOT_ALLOWED = "SIZE_NOT_ALLOWED";

        // System errors
        public const string INTERNAL_ERROR = "INTERNAL_ERROR";
        public const string DATABASE_ERROR = "DATABASE_ERROR";
        public const string NETWORK_ERROR = "NETWORK_ERROR";
    }

    /// <summary>
    /// API error response structure
    /// </summary>
    [Serializable]
    public class PlayKit_ApiErrorResponse
    {
        public PlayKit_ApiError error { get; set; }
    }

    [Serializable]
    public class PlayKit_ApiError
    {
        public string code { get; set; }
        public string message { get; set; }
    }
}