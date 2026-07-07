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
    public async Task<string?> RegisterAsync(RegisterRequest request) =>
        await AuthenticateAsync(() => _http.PostAsJsonAsync("api/auth/register", request), "api/auth/register");
    public async Task<string?> LoginAsync(LoginRequest request) =>
        await AuthenticateAsync(() => _http.PostAsJsonAsync("api/auth/login", request), "api/auth/login");

    // ---- Self-service profile (signed-in user) ----
    public async Task<UserDto?> GetMeAsync() =>
        await SendAsync<UserDto>(() => _http.GetAsync("api/auth/me"));

    /// <summary>Update name/phone. Stores the returned fresh tokens so the nav name updates. Null == success.</summary>
    public async Task<string?> UpdateProfileAsync(UpdateProfileRequest request) =>
        await AuthenticateAsync(() => _http.PutAsJsonAsync("api/auth/profile", request), "api/auth/profile");

    /// <summary>Change password. Stores the returned fresh tokens so the session stays valid. Null == success.</summary>
    public async Task<string?> ChangePasswordAsync(ChangePasswordRequest request) =>
        await AuthenticateAsync(() => _http.PostAsJsonAsync("api/auth/change-password", request), "api/auth/change-password");

    // Runs an auth-shaped request (login/register/profile/password): on success it stores the returned
    // token pair and refreshes auth state; failures return an inline message string (no toast).
    private async Task<string?> AuthenticateAsync(Func<Task<HttpResponseMessage>> send, string url)
    {
        try
        {
            var response = await send();
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

    // ---- Password recovery ----
    // Forgot-password always succeeds server-side (no account enumeration); only a transport failure
    // surfaces (as the shared offline message). Reset returns an inline error string like login/register.
    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request) =>
        await SendOkAsync(() => _http.PostAsJsonAsync("api/auth/forgot-password", request));

    public async Task<string?> ResetPasswordAsync(ResetPasswordWithTokenRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/reset-password", request);
            return response.IsSuccessStatusCode ? null : await ReadErrorMessageAsync(response);
        }
        catch (HttpRequestException ex)
        {
            _errors.Report(OfflineMessage, $"api/auth/reset-password: {ex}");
            return OfflineMessage;
        }
    }

    // ---- Email preferences (unsubscribe management) ----
    public async Task<List<EmailPreferenceDto>> GetEmailPreferencesAsync() =>
        await SendAsync<List<EmailPreferenceDto>>(() => _http.GetAsync("api/email/preferences")) ?? new();

    public async Task<bool> UpdateEmailPreferencesAsync(UpdateEmailPreferencesRequest request) =>
        await SendOkAsync(() => _http.PutAsJsonAsync("api/email/preferences", request));

    // ---- Admin inbox ----
    public async Task<List<InboxMessageDto>> GetInboxAsync() =>
        await SendAsync<List<InboxMessageDto>>(() => _http.GetAsync("api/admin/inbox")) ?? new();

    public async Task<InboxMessageDetailDto?> GetInboxMessageAsync(Guid id) =>
        await SendAsync<InboxMessageDetailDto>(() => _http.GetAsync($"api/admin/inbox/{id}"));

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

    public async Task<List<ProviderSuggestion>> SearchSuggestionsAsync(string term, int take = 8) =>
        await SendAsync<List<ProviderSuggestion>>(
            () => _http.GetAsync($"api/search/typeahead?term={Uri.EscapeDataString(term)}&take={take}")) ?? new();

    public async Task<ProviderDto?> GetProviderAsync(Guid id) =>
        await SendAsync<ProviderDto>(() => _http.GetAsync($"api/providers/{id}"));

    public async Task<ProviderDto?> GetMyProviderAsync() =>
        await SendAsync<ProviderDto>(() => _http.GetAsync("api/providers/me"));

    public async Task<ProviderDto?> SaveMyProviderAsync(SaveProviderRequest request) =>
        await SendAsync<ProviderDto>(() => _http.PutAsJsonAsync("api/providers/me", request));

    // ---- Availability calendar ----
    public async Task<ProviderAvailabilityDto?> GetProviderAvailabilityAsync(Guid providerId) =>
        await SendAsync<ProviderAvailabilityDto>(() => _http.GetAsync($"api/providers/{providerId}/availability"));

    public async Task<bool> SaveAvailabilityAsync(SaveAvailabilityRequest request) =>
        await SendOkAsync(() => _http.PutAsJsonAsync("api/providers/me/availability", request));

    public async Task<TimeOffDto?> AddTimeOffAsync(AddTimeOffRequest request) =>
        await SendAsync<TimeOffDto>(() => _http.PostAsJsonAsync("api/providers/me/time-off", request));

    public async Task<bool> RemoveTimeOffAsync(Guid timeOffId) =>
        await SendOkAsync(() => _http.DeleteAsync($"api/providers/me/time-off/{timeOffId}"));

    // ---- Rate cards ----
    public async Task<List<RateCardDto>> GetProviderRateCardsAsync(Guid providerId) =>
        await SendAsync<List<RateCardDto>>(() => _http.GetAsync($"api/providers/{providerId}/rate-cards")) ?? new();

    public async Task<List<RateCardDto>> GetMyRateCardsAsync() =>
        await SendAsync<List<RateCardDto>>(() => _http.GetAsync("api/providers/me/rate-cards")) ?? new();

    public async Task<RateCardDto?> SaveRateCardAsync(SaveRateCardRequest request) =>
        await SendAsync<RateCardDto>(() => _http.PostAsJsonAsync("api/providers/me/rate-cards", request));

    public async Task<bool> DeleteRateCardAsync(Guid rateCardId) =>
        await SendOkAsync(() => _http.DeleteAsync($"api/providers/me/rate-cards/{rateCardId}"));

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

    // ---- Jobs (bookings, quotes, matching) ----
    public async Task<List<JobRequestDto>> GetMyBookingsAsync() =>
        await SendAsync<List<JobRequestDto>>(() => _http.GetAsync("api/jobs")) ?? new();

    /// <summary>Open jobs the signed-in provider was invited to / matches and can still quote on.</summary>
    public async Task<List<JobRequestDto>> GetOpenJobsAsync() =>
        await SendAsync<List<JobRequestDto>>(() => _http.GetAsync("api/jobs/open")) ?? new();

    public async Task<JobRequestDto?> GetBookingAsync(Guid id) =>
        await SendAsync<JobRequestDto>(() => _http.GetAsync($"api/jobs/{id}"));

    /// <summary>Client requests a booking with a specific provider (direct flow).</summary>
    public async Task<JobRequestDto?> CreateBookingAsync(CreateDirectBookingRequest request) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsJsonAsync("api/jobs/direct", request));

    /// <summary>Client posts a job to gather quotes (targeted or broadcast / urgent).</summary>
    public async Task<JobRequestDto?> PostJobAsync(PostJobRequest request) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsJsonAsync("api/jobs", request));

    public async Task<JobRequestDto?> ProposeBookingTimeAsync(Guid id, DateTime proposedStartUtc) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsJsonAsync($"api/jobs/{id}/propose-time",
            new ProposeTimeRequest { ProposedStartUtc = proposedStartUtc }));

    public async Task<JobRequestDto?> AcceptBookingAsync(Guid id) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsync($"api/jobs/{id}/accept", null));

    public async Task<JobRequestDto?> DeclineBookingAsync(Guid id) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsync($"api/jobs/{id}/decline", null));

    public async Task<JobRequestDto?> CancelBookingAsync(Guid id) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsync($"api/jobs/{id}/cancel", null));

    public async Task<JobRequestDto?> SetBookingLocationAsync(Guid id, SetJobLocationRequest request) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsJsonAsync($"api/jobs/{id}/location", request));

    public async Task<JobRequestDto?> StartBookingAsync(Guid id) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsync($"api/jobs/{id}/start", null));

    public async Task<JobRequestDto?> CompleteBookingAsync(Guid id, CompleteJobRequest request) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsJsonAsync($"api/jobs/{id}/complete", request));

    public async Task<JobRequestDto?> PayBookingAsync(Guid id) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsync($"api/jobs/{id}/pay", null));

    public async Task<JobRequestDto?> FlagNoShowAsync(Guid id) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsync($"api/jobs/{id}/no-show", null));

    public async Task<JobRequestDto?> ReviewBookingAsync(Guid id, CreateJobReviewRequest request) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsJsonAsync($"api/jobs/{id}/review", request));

    // Quotes / matching
    public async Task<List<JobResponseDto>> GetJobResponsesAsync(Guid id) =>
        await SendAsync<List<JobResponseDto>>(() => _http.GetAsync($"api/jobs/{id}/quotes")) ?? new();

    public async Task<JobResponseDto?> SubmitQuoteAsync(Guid id, SubmitQuoteRequest request) =>
        await SendAsync<JobResponseDto>(() => _http.PostAsJsonAsync($"api/jobs/{id}/quotes", request));

    public async Task<bool> WithdrawQuoteAsync(Guid id, Guid responseId) =>
        await SendOkAsync(() => _http.PostAsync($"api/jobs/{id}/quotes/{responseId}/withdraw", null));

    /// <summary>Provider dismisses an open job from their board (no client notification).</summary>
    public async Task<bool> DismissJobAsync(Guid id) =>
        await SendOkAsync(() => _http.PostAsync($"api/jobs/{id}/dismiss", null));

    public async Task<JobRequestDto?> AcceptQuoteAsync(Guid id, Guid responseId) =>
        await SendAsync<JobRequestDto>(() => _http.PostAsync($"api/jobs/{id}/accept-quote/{responseId}", null));

    public async Task<List<JobMessageDto>> GetBookingMessagesAsync(Guid id) =>
        await SendAsync<List<JobMessageDto>>(() => _http.GetAsync($"api/jobs/{id}/messages")) ?? new();

    public async Task<JobMessageDto?> SendBookingMessageAsync(Guid id, string body) =>
        await SendAsync<JobMessageDto>(() => _http.PostAsJsonAsync($"api/jobs/{id}/messages",
            new SendJobMessageRequest { Body = body }));

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
            return _http.PostAsync($"api/jobs/{id}/invoice", form);
        });

    /// <summary>Downloads the booking's invoice file as bytes (for a client-side download trigger).</summary>
    public async Task<FileDownload?> DownloadBookingInvoiceAsync(Guid id, [CallerMemberName] string operation = "")
    {
        try
        {
            using var response = await _http.GetAsync($"api/jobs/{id}/invoice");
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

    /// <summary>Like <see cref="GetNotificationsAsync"/> but returns null on failure (vs. an empty
    /// list), so callers can avoid wiping a good list when a refresh transiently fails.</summary>
    public async Task<List<NotificationDto>?> TryGetNotificationsAsync() =>
        await SendAsync<List<NotificationDto>>(() => _http.GetAsync("api/notifications"));

    public async Task<bool> MarkNotificationReadAsync(Guid id) =>
        await SendOkAsync(() => _http.PostAsync($"api/notifications/{id}/read", null));

    public async Task<bool> MarkAllNotificationsReadAsync() =>
        await SendOkAsync(() => _http.PostAsync("api/notifications/read-all", null));

    // ---- Support / helpdesk ----
    public async Task<List<SupportTicketDto>> GetMyTicketsAsync() =>
        await SendAsync<List<SupportTicketDto>>(() => _http.GetAsync("api/support/tickets/mine")) ?? new();

    public async Task<List<SupportTicketDto>> GetAllTicketsAsync(TicketStatus? status = null, SupportPriority? priority = null)
    {
        var query = new List<string>();
        if (status is { } s) query.Add($"status={(int)s}");
        if (priority is { } p) query.Add($"priority={(int)p}");
        var url = "api/support/tickets" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await SendAsync<List<SupportTicketDto>>(() => _http.GetAsync(url)) ?? new();
    }

    public async Task<SupportTicketDto?> GetTicketAsync(Guid id) =>
        await SendAsync<SupportTicketDto>(() => _http.GetAsync($"api/support/tickets/{id}"));

    public async Task<SupportTicketDto?> CreateTicketAsync(CreateTicketRequest request) =>
        await SendAsync<SupportTicketDto>(() => _http.PostAsJsonAsync("api/support/tickets", request));

    public async Task<SupportTicketDto?> UpdateTicketAsync(Guid id, UpdateTicketRequest request) =>
        await SendAsync<SupportTicketDto>(() => _http.PostAsJsonAsync($"api/support/tickets/{id}", request));

    public async Task<List<SupportMessageDto>> GetTicketMessagesAsync(Guid id) =>
        await SendAsync<List<SupportMessageDto>>(() => _http.GetAsync($"api/support/tickets/{id}/messages")) ?? new();

    public async Task<SupportMessageDto?> PostTicketMessageAsync(Guid id, string body) =>
        await SendAsync<SupportMessageDto>(() => _http.PostAsJsonAsync($"api/support/tickets/{id}/messages",
            new PostSupportMessageRequest { Body = body }));

    /// <summary>Uploads a file to a ticket, optionally tied to a specific message in the thread.</summary>
    public async Task<SupportAttachmentDto?> UploadTicketAttachmentAsync(
        Guid id, Guid? messageId, Stream content, string fileName, string contentType) =>
        await SendAsync<SupportAttachmentDto>(() =>
        {
            var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            if (!string.IsNullOrWhiteSpace(contentType) &&
                System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out var mt))
                fileContent.Headers.ContentType = mt;
            form.Add(fileContent, "file", fileName);
            var url = $"api/support/tickets/{id}/attachments" + (messageId is { } m ? $"?messageId={m}" : "");
            return _http.PostAsync(url, form);
        });

    /// <summary>Downloads a ticket attachment's bytes (for a client-side download trigger).</summary>
    public async Task<FileDownload?> DownloadTicketAttachmentAsync(Guid attachmentId, [CallerMemberName] string operation = "")
    {
        try
        {
            using var response = await _http.GetAsync($"api/support/attachments/{attachmentId}");
            if (!response.IsSuccessStatusCode)
            {
                await ReportFailureAsync(response, operation);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var name = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "attachment";
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            return new FileDownload(name, contentType, bytes);
        }
        catch (HttpRequestException ex)
        {
            _errors.Report(OfflineMessage, $"{operation}: {ex}");
            return null;
        }
    }

    // ---- Admin: users ----
    public async Task<List<AdminUserDto>> GetUsersAsync() =>
        await SendAsync<List<AdminUserDto>>(() => _http.GetAsync("api/admin/users")) ?? new();

    public async Task<bool> SetUserActiveAsync(Guid id, bool isActive) =>
        await SendOkAsync(() => _http.PostAsJsonAsync($"api/admin/users/{id}/active", isActive));

    public async Task<bool> SetUserRoleAsync(Guid id, UserRole role) =>
        await SendOkAsync(() => _http.PostAsJsonAsync($"api/admin/users/{id}/role",
            new UpdateUserRoleRequest { Role = role }));

    public async Task<bool> ResetUserPasswordAsync(Guid id, string newPassword) =>
        await SendOkAsync(() => _http.PostAsJsonAsync($"api/admin/users/{id}/password",
            new ResetPasswordRequest { NewPassword = newPassword }));

    public async Task<List<JobRequestDto>> GetUserBookingsAsync(Guid id) =>
        await SendAsync<List<JobRequestDto>>(() => _http.GetAsync($"api/admin/users/{id}/bookings")) ?? new();

    public async Task<List<SupportTicketDto>> GetUserTicketsAsync(Guid id) =>
        await SendAsync<List<SupportTicketDto>>(() => _http.GetAsync($"api/admin/users/{id}/tickets")) ?? new();

    // ---- Admin ----
    public async Task<RevenueReportDto?> GetRevenueAsync() =>
        await SendAsync<RevenueReportDto>(() => _http.GetAsync("api/admin/revenue"));

    public async Task<bool> SetPlatformCommissionAsync(decimal rate) =>
        await SendOkAsync(() => _http.PostAsJsonAsync("api/admin/commission",
            new SetCommissionRateRequest { Scope = CommissionScope.Platform, Rate = rate }));

    public async Task<bool> SetProviderStatusAsync(Guid providerId, ProviderStatus status) =>
        await SendOkAsync(() => _http.PostAsJsonAsync($"api/admin/providers/{providerId}/status", status));

    public async Task<PlatformSettingsDto?> GetPlatformSettingsAsync() =>
        await SendAsync<PlatformSettingsDto>(() => _http.GetAsync("api/admin/settings"));

    public async Task<bool> UpdatePlatformSettingsAsync(PlatformSettingsDto request) =>
        await SendOkAsync(() => _http.PutAsJsonAsync("api/admin/settings", request));

    // ---- Admin: test emails (preview each mail type) ----
    public async Task<List<TestEmailTypeDto>> GetTestEmailTypesAsync() =>
        await SendAsync<List<TestEmailTypeDto>>(() => _http.GetAsync("api/admin/test-emails/types")) ?? new();

    public async Task<SendTestEmailsResultDto?> SendTestEmailsAsync(SendTestEmailsRequest request) =>
        await SendAsync<SendTestEmailsResultDto>(() => _http.PostAsJsonAsync("api/admin/test-emails", request));

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
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The session has ended (expired/invalid token). Clear the stale token and flip the UI to
            // logged-out rather than surfacing a raw auth error, then let the user know gently.
            await _tokens.ClearAsync();
            _auth.NotifyChanged();
            _errors.Report("Your session has expired. Please log in again.", $"{operation} -> 401 Unauthorized");
            return;
        }

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
