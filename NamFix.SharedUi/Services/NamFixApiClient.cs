using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using NamFix.Shared.Contracts;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Typed HTTP client over the NamFix Web API. UI components depend on this rather than HttpClient
/// directly, so the same components work unchanged when hosted in MAUI against the same API.
///
/// Project rule: EVERY request handles its error. All calls go through <see cref="SendAsync{T}"/> /
/// <see cref="SendOkAsync"/>, which on any non-success response or transport failure report a short
/// message to the user (toast) and log the full detail via <see cref="ApiErrorNotifier"/>.
/// </summary>
public sealed class NamFixApiClient
{
    private const string OfflineMessage = "Can't reach the server. Check your connection and try again.";

    private readonly HttpClient _http;
    private readonly ITokenStore _tokens;
    private readonly NamFixAuthStateProvider _auth;
    private readonly ApiErrorNotifier _errors;

    public NamFixApiClient(HttpClient http, ITokenStore tokens, NamFixAuthStateProvider auth, ApiErrorNotifier errors)
    {
        _http = http;
        _tokens = tokens;
        _auth = auth;
        _errors = errors;
    }

    // ---- Auth ----
    // Auth failures are surfaced inline on the login/register forms (returned string), so they don't
    // also raise a toast; transport failures (server offline) fall back to the shared offline message.
    public async Task<string?> RegisterAsync(RegisterRequest request) => await AuthenticateAsync("api/auth/register", request);
    public async Task<string?> LoginAsync(LoginRequest request) => await AuthenticateAsync("api/auth/login", request);

    private async Task<string?> AuthenticateAsync(string url, object request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(url, request);
            if (!response.IsSuccessStatusCode)
                return await ReadErrorMessageAsync(response);

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) return "Invalid server response.";

            await _tokens.SetTokensAsync(auth.AccessToken, auth.RefreshToken);
            _auth.NotifyChanged();
            return null; // null == success
        }
        catch (HttpRequestException ex)
        {
            _errors.Report(OfflineMessage, $"{url}: {ex}");
            return OfflineMessage;
        }
    }

    public async Task LogoutAsync()
    {
        await _tokens.ClearAsync();
        _auth.NotifyChanged();
    }

    // ---- Taxonomy ----
    public async Task<List<TownDto>> GetTownsAsync() =>
        await SendAsync<List<TownDto>>(() => _http.GetAsync("api/taxonomy/towns")) ?? new();

    public async Task<List<CategoryDto>> GetCategoriesAsync() =>
        await SendAsync<List<CategoryDto>>(() => _http.GetAsync("api/taxonomy/categories")) ?? new();

    public async Task<List<string>> GetTagsAsync() =>
        await SendAsync<List<string>>(() => _http.GetAsync("api/taxonomy/tags")) ?? new();

    // ---- Search & providers ----
    public async Task<PagedResult<ProviderSearchResult>> SearchAsync(ProviderSearchRequest request) =>
        await SendAsync<PagedResult<ProviderSearchResult>>(() => _http.PostAsJsonAsync("api/search", request)) ?? new();

    public async Task<ProviderDto?> GetProviderAsync(Guid id) =>
        await SendAsync<ProviderDto>(() => _http.GetAsync($"api/providers/{id}"));

    public async Task<ProviderDto?> GetMyProviderAsync() =>
        await SendAsync<ProviderDto>(() => _http.GetAsync("api/providers/me"));

    public async Task<ProviderDto?> SaveMyProviderAsync(SaveProviderRequest request) =>
        await SendAsync<ProviderDto>(() => _http.PutAsJsonAsync("api/providers/me", request));

    // ---- Reviews ----
    public async Task<List<ReviewDto>> GetReviewsAsync(Guid providerId) =>
        await SendAsync<List<ReviewDto>>(() => _http.GetAsync($"api/providers/{providerId}/reviews")) ?? new();

    public async Task<bool> AddReviewAsync(CreateReviewRequest request) =>
        await SendOkAsync(() => _http.PostAsJsonAsync($"api/providers/{request.ProviderId}/reviews", request));

    // ---- Transactions ----
    public async Task<TransactionDto?> CreateTransactionAsync(CreateTransactionRequest request) =>
        await SendAsync<TransactionDto>(() => _http.PostAsJsonAsync("api/transactions", request));

    public async Task<ProviderEarningsDto?> GetEarningsAsync() =>
        await SendAsync<ProviderEarningsDto>(() => _http.GetAsync("api/transactions/earnings"));

    // ---- Bookings ----
    public async Task<List<BookingDto>> GetMyBookingsAsync() =>
        await SendAsync<List<BookingDto>>(() => _http.GetAsync("api/bookings")) ?? new();

    public async Task<BookingDto?> GetBookingAsync(Guid id) =>
        await SendAsync<BookingDto>(() => _http.GetAsync($"api/bookings/{id}"));

    public async Task<BookingDto?> CreateBookingAsync(CreateBookingRequest request) =>
        await SendAsync<BookingDto>(() => _http.PostAsJsonAsync("api/bookings", request));

    public async Task<BookingDto?> ProposeBookingTimeAsync(Guid id, DateTime proposedStartUtc) =>
        await SendAsync<BookingDto>(() => _http.PostAsJsonAsync($"api/bookings/{id}/propose-time",
            new ProposeTimeRequest { ProposedStartUtc = proposedStartUtc }));

    public async Task<BookingDto?> AcceptBookingAsync(Guid id) =>
        await SendAsync<BookingDto>(() => _http.PostAsync($"api/bookings/{id}/accept", null));

    public async Task<BookingDto?> DeclineBookingAsync(Guid id) =>
        await SendAsync<BookingDto>(() => _http.PostAsync($"api/bookings/{id}/decline", null));

    public async Task<BookingDto?> CancelBookingAsync(Guid id) =>
        await SendAsync<BookingDto>(() => _http.PostAsync($"api/bookings/{id}/cancel", null));

    public async Task<BookingDto?> SetBookingLocationAsync(Guid id, SetBookingLocationRequest request) =>
        await SendAsync<BookingDto>(() => _http.PostAsJsonAsync($"api/bookings/{id}/location", request));

    public async Task<BookingDto?> CompleteBookingAsync(Guid id, CompleteBookingRequest request) =>
        await SendAsync<BookingDto>(() => _http.PostAsJsonAsync($"api/bookings/{id}/complete", request));

    public async Task<BookingDto?> PayBookingAsync(Guid id) =>
        await SendAsync<BookingDto>(() => _http.PostAsync($"api/bookings/{id}/pay", null));

    public async Task<List<BookingMessageDto>> GetBookingMessagesAsync(Guid id) =>
        await SendAsync<List<BookingMessageDto>>(() => _http.GetAsync($"api/bookings/{id}/messages")) ?? new();

    public async Task<BookingMessageDto?> SendBookingMessageAsync(Guid id, string body) =>
        await SendAsync<BookingMessageDto>(() => _http.PostAsJsonAsync($"api/bookings/{id}/messages",
            new SendBookingMessageRequest { Body = body }));

    /// <summary>Uploads the provider's invoice file (multipart) for a booking.</summary>
    public async Task<bool> UploadBookingInvoiceAsync(Guid id, Stream content, string fileName, string contentType) =>
        await SendOkAsync(() =>
        {
            var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            if (!string.IsNullOrWhiteSpace(contentType) &&
                System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out var mt))
                fileContent.Headers.ContentType = mt;
            form.Add(fileContent, "file", fileName);
            return _http.PostAsync($"api/bookings/{id}/invoice", form);
        });

    /// <summary>Downloads the booking's invoice file as bytes (for a client-side download trigger).</summary>
    public async Task<FileDownload?> DownloadBookingInvoiceAsync(Guid id, [CallerMemberName] string operation = "")
    {
        try
        {
            using var response = await _http.GetAsync($"api/bookings/{id}/invoice");
            if (!response.IsSuccessStatusCode)
            {
                await ReportFailureAsync(response, operation);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var name = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "invoice";
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            return new FileDownload(name, contentType, bytes);
        }
        catch (HttpRequestException ex)
        {
            _errors.Report(OfflineMessage, $"{operation}: {ex}");
            return null;
        }
    }

    // ---- Notifications ----
    public async Task<List<NotificationDto>> GetNotificationsAsync() =>
        await SendAsync<List<NotificationDto>>(() => _http.GetAsync("api/notifications")) ?? new();

    public async Task<bool> MarkNotificationReadAsync(Guid id) =>
        await SendOkAsync(() => _http.PostAsync($"api/notifications/{id}/read", null));

    public async Task<bool> MarkAllNotificationsReadAsync() =>
        await SendOkAsync(() => _http.PostAsync("api/notifications/read-all", null));

    // ---- Admin ----
    public async Task<RevenueReportDto?> GetRevenueAsync() =>
        await SendAsync<RevenueReportDto>(() => _http.GetAsync("api/admin/revenue"));

    public async Task<bool> SetPlatformCommissionAsync(decimal rate) =>
        await SendOkAsync(() => _http.PostAsJsonAsync("api/admin/commission",
            new SetCommissionRateRequest { Scope = CommissionScope.Platform, Rate = rate }));

    public async Task<bool> SetProviderStatusAsync(Guid providerId, ProviderStatus status) =>
        await SendOkAsync(() => _http.PostAsJsonAsync($"api/admin/providers/{providerId}/status", status));

    // ---- Central error-handling plumbing ----

    /// <summary>Sends a request and deserializes <typeparamref name="T"/>; reports + logs any failure.</summary>
    private async Task<T?> SendAsync<T>(Func<Task<HttpResponseMessage>> send, [CallerMemberName] string operation = "")
    {
        try
        {
            using var response = await send();
            if (!response.IsSuccessStatusCode)
            {
                await ReportFailureAsync(response, operation);
                return default;
            }

            if (response.StatusCode == HttpStatusCode.NoContent) return default;
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (HttpRequestException ex)
        {
            _errors.Report(OfflineMessage, $"{operation}: {ex}");
            return default;
        }
    }

    /// <summary>Sends a request expecting only success/failure; reports + logs any failure.</summary>
    private async Task<bool> SendOkAsync(Func<Task<HttpResponseMessage>> send, [CallerMemberName] string operation = "")
    {
        try
        {
            using var response = await send();
            if (response.IsSuccessStatusCode) return true;

            await ReportFailureAsync(response, operation);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _errors.Report(OfflineMessage, $"{operation}: {ex}");
            return false;
        }
    }

    private async Task ReportFailureAsync(HttpResponseMessage response, string operation)
    {
        var message = await ReadErrorMessageAsync(response);
        _errors.Report(message, $"{operation} -> {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    /// <summary>Reads the API's <see cref="ErrorResponse"/> short message, falling back to the status.</summary>
    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            if (!string.IsNullOrWhiteSpace(problem?.Error)) return problem!.Error;
        }
        catch
        {
            // Body wasn't an ErrorResponse (e.g. HTML error page) — fall through to a generic message.
        }

        return $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }
}

/// <summary>A file fetched from the API, ready to hand to a browser download.</summary>
public sealed record FileDownload(string FileName, string ContentType, byte[] Content);
