# Backend GitHub Copilot Prompt: Fix Stripe Checkout Redirect Issue

Copy and paste this entire prompt into GitHub Copilot in your backend repository to get the fix:

---

## Prompt

I have a Stripe Checkout integration in my ASP.NET Core backend where the payment completes successfully but the user is NOT redirected back to my frontend after payment.

### Current Issue
- Frontend sends `successUrl` = `http://localhost:4200/paymob/response` to backend's `/api/payment/create-session` endpoint
- Backend calls Stripe Checkout API but the `SuccessUrl` and `CancelUrl` are either NOT being set or are set to wrong values
- After payment completes (green button shows), user stays on `checkout.stripe.com` instead of being redirected to `http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}`
- I verified via Stripe Dashboard / curl that the session doesn't have a proper `success_url` configured

### What I Need Fixed
1. **Backend must use the `successUrl` from the frontend request** when creating the Stripe Checkout Session
2. **Must append `?session_id={CHECKOUT_SESSION_ID}` placeholder** so Stripe can pass the session ID back to frontend
3. **Set `CancelUrl`** to the provided `cancelUrl` so users can return if they cancel
4. **Both URLs must match the frontend origin** (`http://localhost:4200` for local testing, production domain for prod)
5. **Ensure Stripe session is created with `ClientReferenceId = userId`** so we can identify who paid
6. **Add comprehensive logging** so I can debug if it fails again
7. **Handle both one-time payment (`mode="payment"`) and subscription (`mode="subscription"`) correctly**

### Context
- Frontend URL for success: `http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}`
- Frontend URL for cancel: `http://localhost:4200/paymob/cancel`
- Current backend endpoint: `POST /api/payment/create-session`
- Stripe .NET library version: Stripe.net (latest)
- Database: SQL Server, tracking transactions in `PaymentTransactions` table
- Users have roles; after payment, user's `RoleId` should be updated to Premium (already working via webhook, but checkout redirect broken)

### Example Fix Needed
Show me how to:
1. Accept `successUrl` and `cancelUrl` from the request
2. Pass them to `SessionCreateOptions` with the session ID placeholder
3. Create the LineItems based on `productType`, `billingCycle`, and `amount`
4. Handle subscription mode vs one-time payment mode
5. Store the created Stripe session ID in my `PaymentTransactions` table
6. Return the checkout URL to the frontend
7. Add logging at each step so I can debug

### Code I'm Using
```csharp
// Current method signature (needs fixing):
[HttpPost("create-session")]
[Authorize]
public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
{
    // TODO: Fix this implementation
    // Currently NOT setting SuccessUrl or CancelUrl correctly
    // User doesn't get redirected after payment
}

// Request model:
public class CreateSessionRequest
{
    public string UserId { get; set; }
    public int ProductType { get; set; }
    public int PaymentProvider { get; set; }
    public int Currency { get; set; }
    public int BillingCycle { get; set; }
    public string SuccessUrl { get; set; }  // e.g., "http://localhost:4200/paymob/response"
    public string CancelUrl { get; set; }   // e.g., "http://localhost:4200/paymob/cancel"
}

// Response model:
public class CreateSessionResponse
{
    public string TransactionId { get; set; }
    public string ProviderReference { get; set; }  // Stripe session ID
    public string CheckoutUrl { get; set; }        // Stripe Checkout URL
    public decimal Amount { get; set; }
    public int Currency { get; set; }
    public string ExpiresAt { get; set; }
}
```

### Stripe Session Creation Requirements
- `Mode`: "payment" for one-time OR "subscription" for recurring
- `SuccessUrl`: Must include `?session_id={CHECKOUT_SESSION_ID}` placeholder
- `CancelUrl`: Simple cancel URL
- `ClientReferenceId`: User ID (so webhook knows who paid)
- `LineItems`: Product name, description, price, currency
- For subscriptions: include `Recurring` with `Interval = "month"` and `IntervalCount = 1`
- For one-time: no recurring interval

### Important Notes
- DO NOT hardcode URLs; use values from the request
- DO test with: SuccessUrl = `http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}`
- DO convert amount to cents for Stripe (multiply by 100)
- DO use "egp" currency for Egyptian Pound test
- DO add try-catch with proper error handling and logging
- DO log the created session ID and URLs so I can verify they're correct
- DO NOT return before the redirect URLs are verified as set in Stripe

### After Fix
- Frontend will receive `CheckoutUrl` from this endpoint
- Frontend navigates with `window.location.href = checkoutUrl`
- User completes payment on Stripe
- Stripe redirects to `http://localhost:4200/paymob/response?session_id=cs_test_...`
- Frontend's `/paymob/response` route receives the `session_id` query parameter
- Frontend calls `/api/payment/verify` with the session ID
- Backend verifies payment and updates user's `RoleId` to Premium
- User sees premium features unlocked

Show me the complete, production-ready implementation of this endpoint.

---

## How to Use This Prompt

1. Open GitHub Copilot in your Visual Studio or VS Code (backend repository)
2. Create a new chat or continue in an existing one
3. Paste the entire prompt above
4. Click "Send" or "Generate"
5. Copilot will provide the fixed implementation
6. Review the code and apply it to your `CreateSession` endpoint
7. Test with Stripe test card `4242 4242 4242 4242`
8. Verify redirect happens after payment completes

## Expected Output

Copilot should generate code that:
- ✅ Sets `SessionCreateOptions.SuccessUrl` correctly
- ✅ Sets `SessionCreateOptions.CancelUrl` correctly
- ✅ Includes proper error handling
- ✅ Logs the session creation process
- ✅ Returns `CheckoutUrl` to frontend
- ✅ Works for both one-time and subscription payments
- ✅ Stores session ID in database for later verification

## If Copilot's Answer Isn't Complete

Ask follow-up questions like:
- "How do I convert the amount to cents for Stripe?"
- "Show me how to handle subscription vs one-time payment in LineItems"
- "Add more detailed logging so I can see the success_url being set"
- "What do I need to do if the session creation fails?"

---
