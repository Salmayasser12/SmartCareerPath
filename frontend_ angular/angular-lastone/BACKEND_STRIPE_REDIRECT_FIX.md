# BACKEND: Fix Stripe Redirect After Payment (CRITICAL)

## Problem
After user completes Stripe Checkout payment, Stripe is NOT redirecting the browser back to the frontend. The user stays on the Stripe page or gets an error.

## Root Cause
The backend is NOT passing the correct `success_url` and `cancel_url` to Stripe's `checkout.sessions.create()` call. Stripe redirects to these URLs after payment, so if they're missing or malformed, the redirect fails.

## Solution: Backend Stripe Session Configuration

### What the Frontend Sends to Backend
When the frontend calls `POST /api/payment/create-session`, it sends:
```json
{
  "productType": 1,
  "paymentProvider": 1,
  "currency": 1,
  "billingCycle": 1,
  "successUrl": "http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}",
  "cancelUrl": "http://localhost:4200/paymob/cancel",
  "userId": "user-id-from-token"
}
```

### What Backend MUST Do
In your `CreateCheckoutSession` endpoint (or wherever you call `stripe.checkout.sessions.create`):

#### Step 1: Extract successUrl and cancelUrl from the request
```csharp
// In your CreateSessionRequest DTO or similar
public class CreateSessionRequest
{
    public int ProductType { get; set; }
    public int PaymentProvider { get; set; }
    public int Currency { get; set; }
    public int BillingCycle { get; set; }
    public string SuccessUrl { get; set; }  // ← CRITICAL: Frontend provides this
    public string CancelUrl { get; set; }   // ← CRITICAL: Frontend provides this
    public string UserId { get; set; }
}
```

#### Step 2: Pass these URLs directly to Stripe (DO NOT modify them)
```csharp
var options = new SessionCreateOptions
{
    PaymentMethodTypes = new List<string> { "card" },
    Mode = "subscription", // or "payment" depending on your setup
    SuccessUrl = request.SuccessUrl,   // ← Use the URL from the request directly
    CancelUrl = request.CancelUrl,     // ← Use the URL from the request directly
    LineItems = new List<SessionLineItemOptions>
    {
        new SessionLineItemOptions
        {
            // ... product/price details ...
        }
    }
};

var session = await client.Sessions.CreateAsync(options);
```

#### Step 3: Return the session details to the frontend
```csharp
return new CreateSessionResponse
{
    TransactionId = session.Id,
    ProviderReference = session.Id,  // Stripe session ID (cs_test_...)
    CheckoutUrl = session.Url,       // The Stripe-hosted checkout URL
    Amount = (long)(request.Amount * 100), // in cents
    Currency = "EGP", // or whatever currency code you use
    ExpiresAt = session.ExpiresAt
};
```

### CRITICAL CHECKS
1. **SuccessUrl must include `{CHECKOUT_SESSION_ID}` placeholder**
   - Frontend sends: `http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}`
   - Stripe will replace `{CHECKOUT_SESSION_ID}` with the real session id
   - Backend should NOT modify this — pass it as-is to Stripe

2. **Both URLs must be HTTPS in production**
   - Local dev: `http://localhost:4200/paymob/response` ✓
   - Production: `https://yourdomain.com/paymob/response` ✓
   - Mixed HTTP/HTTPS will fail

3. **Do NOT hardcode URLs in backend**
   - ❌ Bad: `string successUrl = "https://myapp.com/payment/success"`
   - ✓ Good: `SuccessUrl = request.SuccessUrl` (trust frontend to provide correct URL)

4. **Webhook Setup** (for payment verification, independent of redirect)
   - Configure webhook endpoint: `POST /api/payment/webhook`
   - Webhook secret: store in environment variable, not in code
   - Listen for `charge.succeeded` or `payment_intent.succeeded` events
   - Update user role in database when webhook confirms payment

### Debug Steps for Backend Team
1. Add logging to the session.create call:
   ```csharp
   Console.WriteLine($"[Stripe] Creating session with:");
   Console.WriteLine($"  SuccessUrl: {options.SuccessUrl}");
   Console.WriteLine($"  CancelUrl: {options.CancelUrl}");
   Console.WriteLine($"  Mode: {options.Mode}");
   Console.WriteLine($"  LineItems: {options.LineItems.Count}");
   
   var session = await client.Sessions.CreateAsync(options);
   
   Console.WriteLine($"[Stripe] Session created:");
   Console.WriteLine($"  SessionId: {session.Id}");
   Console.WriteLine($"  Url: {session.Url}");
   ```

2. Verify in Stripe Dashboard (test environment):
   - Go to Payments → Checkout Sessions
   - Find the session you just created
   - Check the "Success URL" and "Cancel URL" fields — they should match what you sent

3. Test the full flow:
   - Frontend clicks Buy
   - Check backend logs for the session creation
   - Open the returned `session.Url` in a browser
   - Complete payment with `4242 4242 4242 4242`
   - Verify browser redirects to `http://localhost:4200/paymob/response?session_id=cs_test_...`

### Common Mistakes (AVOID)
- ❌ Not passing `successUrl`/`cancelUrl` from frontend to Stripe
- ❌ Hardcoding URLs instead of using the ones from the request
- ❌ Including extra query params that break the placeholder: `?session_id={CHECKOUT_SESSION_ID}&extra=value` (OK, but watch for URL encoding)
- ❌ Using HTTP when Stripe expects HTTPS (production only)
- ❌ Not storing the session ID in the database (need it for webhook verification later)
- ❌ Assuming redirect happens automatically without confirming Stripe URLs are set correctly

### Integration Checklist
- [ ] Backend extracts `successUrl` and `cancelUrl` from the request
- [ ] Backend passes these URLs directly to `stripe.checkout.sessions.create()`
- [ ] Backend returns `session.Url` as `checkoutUrl` in the response
- [ ] Backend returns `session.Id` as `providerReference` in the response
- [ ] Frontend receives and stores the provider reference
- [ ] Frontend opens the checkout URL with `window.location.href = checkoutUrl`
- [ ] After payment, browser redirects to `successUrl?session_id={CHECKOUT_SESSION_ID}`
- [ ] Frontend success page reads `session_id` from URL and calls `/api/payment/verify`
- [ ] Backend verify endpoint confirms payment and updates user role

### Frontend Expectations (DO NOT CHANGE)
- Success page URL: `/paymob/response?session_id={session_id}`
- Cancel page URL: `/paymob/cancel`
- Frontend will poll `/api/payment/verify` with `providerReference` to confirm payment
- Backend verify endpoint must return a response with `status`, `verified`, `success`, or similar flag

---

## Copy This Prompt and Send to Backend Team
If using GitHub Copilot or similar:

> I need to fix Stripe payment redirect. After user completes checkout, Stripe should redirect to my frontend success page. Right now it's not redirecting.
> 
> My frontend sends: `POST /api/payment/create-session` with `{ successUrl: "http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}", cancelUrl: "..." }`
> 
> My backend needs to:
> 1. Accept `successUrl` and `cancelUrl` in the request
> 2. Pass them directly to `stripe.checkout.sessions.create()`
> 3. Return the session URL and ID to the frontend
> 
> Can you write the code to do this? Include logging so I can debug. Make sure the Stripe URLs match what the frontend sent.

---

## Reference: What Your Frontend Already Does
- ✓ Tracks session ID before opening Stripe: `paymentService.trackCheckoutSession(sessionId)`
- ✓ Sends correct `successUrl` with `{CHECKOUT_SESSION_ID}` placeholder
- ✓ Polls `/api/payment/verify` every 2 seconds for up to 30 seconds
- ✓ Calls `AuthService.refreshUserProfile()` to grab updated user role from backend
- ✓ Has fallback logic for Safari ITP and missing query params

The ONLY missing piece is: **backend must pass the URLs to Stripe**.
