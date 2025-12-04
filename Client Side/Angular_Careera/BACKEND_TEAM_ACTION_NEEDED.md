# For Your Backend Team: Stripe Checkout Redirect Fix

Copy this message and send to your backend developer:

---

## Issue
After Stripe payment completes, users are NOT redirected back to the Angular app. They stay stuck on `checkout.stripe.com`.

## Root Cause
The `/api/payment/create-session` endpoint is not setting the `SuccessUrl` when creating the Stripe Checkout Session.

Stripe needs to know where to redirect **when the session is created**, not after.

## What Needs To Be Fixed

In your backend's `CreateSession` method (wherever you create Stripe Checkout sessions), you must:

### BEFORE (Current - Broken)
```csharp
var sessionOptions = new SessionCreateOptions
{
    ClientReferenceId = user.Id,
    PaymentMethodTypes = new List<string> { "card" },
    Mode = "subscription",
    LineItems = new List<SessionLineItemOptions> { /* ... */ }
    // ❌ Missing: SuccessUrl and CancelUrl
};
```

### AFTER (Fixed)
```csharp
var sessionOptions = new SessionCreateOptions
{
    ClientReferenceId = user.Id,
    PaymentMethodTypes = new List<string> { "card" },
    Mode = "subscription",
    
    // ✅ ADD THESE TWO LINES:
    SuccessUrl = $"{request.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
    CancelUrl = request.CancelUrl,
    
    LineItems = new List<SessionLineItemOptions> { /* ... */ }
};
```

## Key Details

1. **Accept redirect URLs from the frontend request:**
   ```csharp
   public class CreateSessionRequest
   {
       public string SuccessUrl { get; set; }  // e.g., "http://localhost:4200/paymob/response"
       public string CancelUrl { get; set; }   // e.g., "http://localhost:4200/paymob/cancel"
       // ... other fields ...
   }
   ```

2. **Append the session ID placeholder to success URL:**
   ```csharp
   var finalSuccessUrl = $"{request.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}";
   // Result: "http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}"
   // ^ Stripe will replace {CHECKOUT_SESSION_ID} with actual session ID
   ```

3. **Pass both URLs to SessionCreateOptions:**
   ```csharp
   var sessionOptions = new SessionCreateOptions
   {
       SuccessUrl = finalSuccessUrl,  // ← CRITICAL
       CancelUrl = request.CancelUrl,  // ← CRITICAL
       // ... other options
   };
   ```

## Why This Matters

- **Stripe Checkout** is a hosted page that Stripe controls completely
- When payment completes, Stripe redirects the user's browser to `success_url`
- If `success_url` is not set, Stripe doesn't know where to send users
- Frontend can't fix this — only backend can tell Stripe where to redirect

## Verification Steps

After making the fix:

1. **Add logging** to verify URLs are being set:
   ```csharp
   var service = new SessionService();
   var session = await service.CreateAsync(sessionOptions);
   
   Console.WriteLine($"Session ID: {session.Id}");
   Console.WriteLine($"Success URL: {session.SuccessUrl}");  // Should show with the full URL
   Console.WriteLine($"Cancel URL: {session.CancelUrl}");
   ```

2. **Test the payment:**
   - Navigate to `/plans`
   - Click "Subscribe"
   - Use test card: `4242 4242 4242 4242` / `12/34` / `123`
   - Complete payment
   - Expected: Browser redirects to `/paymob/response?session_id=cs_test_...` ✓

3. **Verify in Stripe Dashboard:**
   - Go to Payments → Checkout Sessions
   - Find the session by ID
   - Check `success_url` field contains your frontend URL

## If Using GitHub Copilot

If you want Copilot to help, use the prompt from `BACKEND_STRIPE_FIX_PROMPT.md`.

## Complete Code Example

```csharp
[HttpPost("create-session")]
[Authorize]
public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
{
    try
    {
        var user = await _userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        if (user == null) return Unauthorized();

        // ✅ Use the URLs from frontend request
        var successUrl = $"{request.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = request.CancelUrl;
        
        Console.WriteLine($"[CreateSession] Creating session...");
        Console.WriteLine($"  Success URL: {successUrl}");
        Console.WriteLine($"  Cancel URL: {cancelUrl}");

        var sessionOptions = new SessionCreateOptions
        {
            ClientReferenceId = user.Id,
            
            // ✅ THIS IS THE FIX:
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "egp",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Careera Pro Premium",
                            Description = "Access all premium features"
                        },
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = "month"
                        },
                        UnitAmountDecimal = 3000  // 30 EGP = 3000 cents
                    },
                    Quantity = 1
                }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(sessionOptions);
        
        Console.WriteLine($"[CreateSession] ✓ Session created: {session.Id}");
        Console.WriteLine($"[CreateSession] ✓ Success URL in Stripe: {session.SuccessUrl}");

        // Store in DB for verification later
        var transaction = new PaymentTransaction
        {
            UserId = user.Id,
            ProviderReference = session.Id,
            TransactionId = Guid.NewGuid().ToString(),
            Amount = 3000,
            Currency = "EGP",
            Status = "pending"
        };
        _context.PaymentTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return Ok(new CreateSessionResponse
        {
            TransactionId = transaction.TransactionId,
            ProviderReference = session.Id,
            CheckoutUrl = session.Url,
            Amount = 30,
            Currency = 1
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CreateSession] Error: {ex.Message}");
        return BadRequest(new { message = ex.Message });
    }
}
```

## Summary

- **Add:** `SuccessUrl` to SessionCreateOptions
- **Add:** `CancelUrl` to SessionCreateOptions
- **Format:** Include `?session_id={CHECKOUT_SESSION_ID}` in success URL
- **Test:** Payment should redirect after completion
- **Deploy:** Restart backend and test

That's it! Everything else in the system is working.

---

Questions? Files available:
- `STRIPE_COMPLETE_SOLUTION.md` — Overview + code
- `STRIPE_REDIRECT_FIX.md` — Detailed C# examples
- `STRIPE_TESTING_COMMANDS.md` — How to verify the fix
