using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SmartCareerPath.Application.Abstraction.DTOs.Payment;
using SmartCareerPath.Application.Abstraction.ServicesContracts.Payment;
using SmartCareerPath.Application.Abstraction.ServicesContracts.Auth;
using SmartCareerPath.Domain.Contracts;
using SmartCareerPath.Domain.Enums;
using SmartCareerPath.Domain.Entities.Auth;

namespace SmartCareerPath.APIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        public PaymentController(
            IPaymentService paymentService,
            ILogger<PaymentController> logger,
            ITokenService tokenService,
            IUnitOfWork unitOfWork)
        {
            _paymentService = paymentService;
            _logger = logger;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
        }



        [HttpPost("create-session")]
        [Authorize]
        [ProducesResponseType(typeof(PaymentSessionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreatePaymentSession(
            [FromBody] CreatePaymentSessionRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== CreatePaymentSession Request Started ===");

            // Log incoming request body
            if (request != null)
            {
                _logger.LogInformation(
                    "Request payload: UserId={UserId}, ProductType={ProductType}, PaymentProvider={PaymentProvider}, Currency={Currency}, BillingCycle={BillingCycle}, SuccessUrl={SuccessUrl}, CancelUrl={CancelUrl}",
                    request.UserId, request.ProductType, request.PaymentProvider, request.Currency,
                    request.BillingCycle, request.SuccessUrl, request.CancelUrl);
            }

            // Log model state validation errors
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed");
                var validationErrors = new Dictionary<string, List<string>>();

                foreach (var kvp in ModelState)
                {
                    var fieldName = kvp.Key;
                    var modelState = kvp.Value;
                    
                    foreach (var error in modelState.Errors)
                    {
                        var errorMessage = error.ErrorMessage;
                        
                        _logger.LogWarning("Validation error - Field: {field}, Message: {message}", fieldName, errorMessage);

                        if (!validationErrors.ContainsKey(fieldName))
                        {
                            validationErrors[fieldName] = new List<string>();
                        }
                        validationErrors[fieldName].Add(errorMessage);
                    }
                }

                return BadRequest(new
                {
                    error = "Validation failed",
                    details = validationErrors,
                    message = "One or more validation errors occurred"
                });
            }

            // Log Authorization header for debugging
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation("Authorization header present: {hasAuth}", !string.IsNullOrEmpty(authHeader));

            string token = null;
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
                _logger.LogInformation("Extracted token length: {len}", token.Length);
            }

            // If authenticated, log claims and try to extract user id from token claims
            if (User?.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("User is authenticated");
                foreach (var claim in User.Claims)
                {
                    _logger.LogInformation("Claim: {type} = {value}", claim.Type, claim.Value);
                }

                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub") ?? User.FindFirst("nameid");
                if (idClaim != null && int.TryParse(idClaim.Value, out var parsedUserId) && parsedUserId > 0)
                {
                    request.UserId = parsedUserId; // prefer token user id
                    _logger.LogInformation("Using userId from token: {userId}", parsedUserId);
                }
            }
            else
            {
                // No authenticated user - return structured JSON error
                _logger.LogWarning("User not authenticated");
                return Unauthorized(new { error = "Unauthorized", message = "Missing or invalid token" });
            }

            try
            {
                _logger.LogInformation("Calling PaymentService.CreatePaymentSessionAsync with request: UserId={UserId}, ProductType={ProductType}, PaymentProvider={PaymentProvider}",
                    request.UserId, request.ProductType, request.PaymentProvider);

                var result = await _paymentService.CreatePaymentSessionAsync(request, cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogError("Payment service returned failure: {error}", result.Error);
                    return BadRequest(new
                    {
                        error = "Payment session creation failed",
                        details = new { reason = result.Error },
                        message = result.Error
                    });
                }

                _logger.LogInformation("Payment session created successfully. TransactionId: {transactionId}",
                    result.Value?.TransactionId);

                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while creating payment session. Exception type: {exceptionType}, Message: {message}",
                    ex.GetType().Name, ex.Message);

                return BadRequest(new
                {
                    error = "An exception occurred",
                    details = new
                    {
                        exceptionType = ex.GetType().Name,
                        message = ex.Message,
                        stackTrace = ex.StackTrace
                    },
                    message = "Payment session creation failed due to an unexpected error"
                });
            }
        }



        // Verify Payment (Webhook Handler)
        [HttpPost("verify")]
        [AllowAnonymous] // Webhooks come from external providers
        [ProducesResponseType(typeof(PaymentVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> VerifyPayment(
            [FromBody] VerifyPaymentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _paymentService.VerifyPaymentAsync(request, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(new { error = result.Error });
            }

            var response = result.Value;

            // If payment was completed, generate a new JWT token with updated role
            // This allows the frontend to immediately have the Premium role without signing out/in
            if (response.Status == "Completed" && response.UserId > 0)
            {
                try
                {
                    _logger.LogInformation("Payment {TransactionId} completed. Generating new token with updated role.", response.TransactionId);
                    
                    // The payment service should have fetched the user's updated role.
                    // We need the UserId to get the user and generate a new token.
                    // For now, we'll attempt to get this from the payment transaction or add it to the response.
                    response.Token = await GenerateUpdatedTokenForPaymentAsync(response.UserId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate updated token for payment {TransactionId}. Frontend will still work but may need to sign out/in.", response.TransactionId);
                    // Don't fail the response - frontend can still work without the token
                }
            }

            return Ok(response);
        }

        /// <summary>
        /// Generate a new JWT token for the user after payment completion.
        /// This allows the frontend to immediately have updated permissions.
        /// </summary>
        private async Task<string?> GenerateUpdatedTokenForPaymentAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Fetching user {UserId} to generate updated token", userId);
                
                // Fetch the user to get their email and current role
                var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found for token generation", userId);
                    return null;
                }

                // Get the user's current role
                var roleName = user.Role?.Name ?? "User";

                _logger.LogInformation("Generating new token for user {UserId} with role {RoleName}", userId, roleName);
                
                // Generate a new JWT token with the updated role
                var token = _tokenService.GenerateAccessToken(userId, user.Email ?? "", roleName);
                
                _logger.LogInformation("Successfully generated updated token for user {UserId}", userId);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating updated token for user");
                return null;
            }
        }


        // Stripe Webhook entrypoint (Stripe posts raw event JSON to this endpoint)
        [HttpPost("stripe/webhook")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
        {
            // Read raw payload
            string payload;
            using (var reader = new System.IO.StreamReader(Request.Body))
            {
                payload = await reader.ReadToEndAsync();
            }

            var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

            _logger.LogInformation("Received Stripe webhook. Signature present: {hasSig}", !string.IsNullOrEmpty(signature));

            var handleResult = await _paymentService.HandleWebhookEventAsync((int)PaymentProvider.Stripe, payload, signature, cancellationToken);

            if (handleResult.IsFailure)
            {
                _logger.LogWarning("Stripe webhook handling failed: {error}", handleResult.Error);
                return BadRequest(new { error = handleResult.Error });
            }

            return Ok();
        }


        // Get Payment Details
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(PaymentTransactionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPaymentById(
            int id,
            CancellationToken cancellationToken)
        {
            var result = await _paymentService.GetPaymentByIdAsync(id, cancellationToken);

            if (result.IsFailure)
            {
                return NotFound(new { error = result.Error });
            }

            return Ok(result.Value);
        }


        // Get User Payment History
        [HttpGet("history/{userId}")]
        [Authorize]
        [ProducesResponseType(typeof(PaginatedResponse<PaymentHistoryItemResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPaymentHistory(
            int userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await _paymentService.GetUserPaymentHistoryAsync(
                userId, pageNumber, pageSize, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(result.Value);
        }

        // Create Refund Request
        [HttpPost("refund-request")]
        [Authorize]
        [ProducesResponseType(typeof(RefundRequestResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateRefundRequest(
            [FromBody] CreateRefundRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _paymentService.CreateRefundRequestAsync(request, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(new { error = result.Error });
            }

            return StatusCode(201, result.Value);
        }


        // Get Product Pricing
        [HttpGet("pricing")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ProductPricingResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProductPricing(
            [FromQuery] int currency = 1, // Default USD
            CancellationToken cancellationToken = default)
        {
            var result = await _paymentService.GetProductPricingAsync(currency, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(result.Value);
        }


        // Stripe Redirect Handler - Auto-redirects from Stripe back to Angular payment-success component
        [HttpGet("stripe/redirect")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult StripeRedirect([FromQuery] string session_id)
        {
            if (string.IsNullOrEmpty(session_id))
            {
                _logger.LogWarning("[StripeRedirect] No session_id provided in query string");
                return Redirect("/paymob/response");
            }

            _logger.LogInformation("[StripeRedirect] Received redirect with session_id: {SessionId}", session_id);
            // Attempt to verify payment server-side so user's role is activated immediately.
            // This also allows us to generate a fresh JWT and inject it into the browser sessionStorage
            // so the frontend immediately observes the Premium role.
            string? injectedToken = null;
            try
            {
                var verifyReq = new SmartCareerPath.Application.Abstraction.DTOs.Payment.VerifyPaymentRequest
                {
                    ProviderReference = session_id
                };

                var verifyResultTask = _paymentService.VerifyPaymentAsync(verifyReq, CancellationToken.None);
                verifyResultTask.Wait();
                var verifyResult = verifyResultTask.Result;

                if (verifyResult.IsSuccess)
                {
                    var resp = verifyResult.Value;
                    // Generate updated token for frontend if possible
                    if (resp.UserId > 0 && resp.Status == "Completed")
                    {
                        try
                        {
                            var tokenTask = GenerateUpdatedTokenForPaymentAsync(resp.UserId, CancellationToken.None);
                            tokenTask.Wait();
                            injectedToken = tokenTask.Result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to generate injected token after verify in StripeRedirect");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("VerifyPaymentAsync returned failure when called from StripeRedirect: {Error}", verifyResult.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while attempting to VerifyPaymentAsync in StripeRedirect");
            }

            // Return HTML page that auto-redirects to Angular payment-success component
            var redirectUrl = $"/paymob/response?session_id={Uri.EscapeDataString(session_id)}";
            // Build HTML that injects sessionStorage values (tracked session & token) before navigating
            var tokenScript = string.Empty;
            if (!string.IsNullOrEmpty(injectedToken))
            {
                // Store token into sessionStorage under the key used by AuthService
                var escapedToken = System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(injectedToken);
                tokenScript = $"try{{ sessionStorage.setItem('scp_auth_token','{escapedToken}'); console.log('[StripeRedirect] Injected token into sessionStorage'); }}catch(e){{console.warn('token inject failed', e);}}";
            }

            var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Payment Processing</title>
    <style>
        body {{
            margin: 0;
            padding: 0;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif;
            background: linear-gradient(to bottom right, #f5f5f5, #e0e0e0);
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
        }}
        .container {{
            text-align: center;
            padding: 20px;
            background: white;
            border-radius: 8px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            max-width: 400px;
        }}
        .spinner {{
            display: inline-block;
            width: 40px;
            height: 40px;
            border: 4px solid #f3f3f3;
            border-top: 4px solid #3498db;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin-bottom: 20px;
        }}
        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}
        h1 {{
            font-size: 24px;
            margin: 0 0 10px 0;
            color: #333;
        }}
        p {{
            color: #666;
            margin: 0;
            font-size: 14px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='spinner'></div>
        <h1>Processing Payment</h1>
        <p>Redirecting you to verify your payment...</p>
    </div>
    <script>
        // Store tracked session id so the Angular payment-success component can verify immediately
        try {{ sessionStorage.setItem('last_checkout_session_id', '{Uri.EscapeDataString(session_id)}'); console.log('[StripeRedirect] Stored last_checkout_session_id in sessionStorage'); }} catch(e) {{ console.warn('[StripeRedirect] Failed to store session id', e); }}
        {tokenScript}
        // Redirect to Angular payment-success component after a short delay
        setTimeout(function() {{
            window.location.href = '{redirectUrl}';
        }}, 150);
    </script>
</body>
</html>";

            return Content(html, "text/html; charset=utf-8");
        }


        // Health Check
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                service = "Payment Service",
                status = "Healthy",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
