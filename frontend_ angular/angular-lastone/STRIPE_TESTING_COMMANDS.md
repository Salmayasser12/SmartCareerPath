# Stripe Redirect Testing - Commands & Verification

## Quick Diagnosis: Run This Now

### 1. Get Your Stripe Secret Key
Find your Stripe test secret key (looks like `sk_test_...`) from Stripe Dashboard

### 2. Get Your Session ID
From your most recent payment attempt:
```javascript
// In browser console:
sessionStorage.getItem('stripeSessionId')
```

### 3. Inspect the Stripe Session
Replace `sk_test_YOUR_KEY` and `cs_test_YOUR_SESSION_ID`:
```bash
curl -u sk_test_YOUR_KEY: \
  "https://api.stripe.com/v1/checkout/sessions/cs_test_YOUR_SESSION_ID"
```

### 4. Look for This in the Response
```json
{
  "id": "cs_test_YOUR_SESSION_ID",
  "success_url": "http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}",
  "cancel_url": "http://localhost:4200/paymob/cancel",
  "status": "complete",
  "payment_status": "paid"
}
```

**If `success_url` is missing or wrong, the backend needs fixing.**

---

## Backend Testing Script

After backend is updated, run these commands to verify:

### Test 1: Call create-session endpoint
```bash
# Get your JWT token from browser:
# localStorage.getItem('scp_auth_token') or sessionStorage.getItem('scp_auth_token')

TOKEN="your_jwt_token_here"

curl -X POST "http://localhost:5164/api/payment/create-session" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productType": 1,
    "paymentProvider": 1,
    "currency": 1,
    "billingCycle": 1,
    "successUrl": "http://localhost:4200/paymob/response",
    "cancelUrl": "http://localhost:4200/paymob/cancel"
  }'
```

Expected response:
```json
{
  "transactionId": "some-id",
  "providerReference": "cs_test_...",
  "checkoutUrl": "https://checkout.stripe.com/pay/cs_test_...",
  "amount": 30,
  "currency": 1
}
```

### Test 2: Check backend logs for success URL
Look in backend console for:
```
[CreateSession] Success URL configured: http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}
```

If this log doesn't appear, the backend code changes weren't applied.

### Test 3: Query Stripe to verify session was created with URLs
```bash
SK_KEY="sk_test_YOUR_KEY"
SESSION_ID="cs_test_from_response_above"

curl -u $SK_KEY: "https://api.stripe.com/v1/checkout/sessions/$SESSION_ID" | jq '.success_url, .cancel_url, .payment_status'
```

Should return:
```
"http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}"
"http://localhost:4200/paymob/cancel"
"unpaid"  (until payment is made)
```

### Test 4: Simulate payment verification
After making a test payment, call the verify endpoint:
```bash
curl -X POST "http://localhost:5164/api/payment/verify" \
  -H "Content-Type: application/json" \
  -d '{
    "providerReference": "cs_test_YOUR_SESSION_ID",
    "request": {}
  }'
```

Expected response:
```json
{
  "verified": true,
  "message": "Payment verified successfully"
}
```

---

## Frontend Testing (Browser Console)

### Check 1: Verify payment_transaction_raw was stored
```javascript
const raw = JSON.parse(sessionStorage.getItem('payment_transaction_raw') || 'null');
console.log('Check checkoutUrl contains stripe.com:', raw?.checkoutUrl?.includes('stripe.com'));
console.log('Checkout URL:', raw?.checkoutUrl);
console.log('Provider Reference:', raw?.providerReference);
```

### Check 2: Verify session_id extracted from URL after redirect
```javascript
// After redirect, should have session_id in URL
const urlParams = new URLSearchParams(window.location.search);
console.log('Session ID from URL:', urlParams.get('session_id'));
console.log('Current URL:', window.location.href);
```

### Check 3: Verify verify endpoint was called
```javascript
// Open Network tab in DevTools
// Look for POST request to: http://localhost:5164/api/payment/verify
// Request body should contain: {"providerReference":"cs_test_...","request":{}}
// Response status should be: 200
```

---

## Database Verification

After payment flow completes, run these SQL queries:

### Check transaction was recorded
```sql
SELECT * FROM PaymentTransactions 
WHERE ProviderReference = 'cs_test_YOUR_SESSION_ID';
```

Should show:
- `UserId` (who paid)
- `ProviderReference` (cs_test_...)
- `Amount` (30)
- `Status` (should be 'verified' or 'completed')

### Check user role was updated
```sql
SELECT Id, Email, RoleId, CreatedAt FROM Users 
WHERE Id = 'USER_ID_FROM_PAYMENT';
```

- `RoleId` should be updated to Premium role ID
- Compare with a non-premium user to see the difference

### Check subscription was created (if using subscriptions)
```sql
SELECT * FROM UserSubscriptions 
WHERE UserId = 'USER_ID';
```

Should show:
- `StripeSubscriptionId`
- `Status` (should be 'active')
- `CurrentPeriodEnd` (future date)

---

## Troubleshooting Checklist

### Payment completes but NO redirect happens
- [ ] Check backend logs for `[CreateSession] Success URL` message
- [ ] Verify Stripe Dashboard shows correct `success_url`
- [ ] Ensure `success_url` includes `?session_id={CHECKOUT_SESSION_ID}`
- [ ] Check if URL exactly matches: `http://localhost:4200/paymob/response`

### Redirect happens but success page shows error
- [ ] Verify `/paymob/response` route exists in Angular routing
- [ ] Check Network tab for `/api/payment/verify` call and response
- [ ] Look at browser console for error logs with `[PaymentSuccess]` prefix

### Session ID shows as null/undefined
- [ ] Check URL has `?session_id=cs_test_...`
- [ ] Verify backend appended placeholder correctly: `?session_id={CHECKOUT_SESSION_ID}`
- [ ] Check query parameter parsing in Angular component

### Verify endpoint returns 400/415
- [ ] Check request headers include `Content-Type: application/json`
- [ ] Verify request body matches: `{ "providerReference": "cs_test_...", "request": {} }`
- [ ] Ensure no Authorization header is sent (verify endpoint is AllowAnonymous)

### Role not updating after payment
- [ ] Check verify endpoint is calling the role update logic
- [ ] Verify backend's `getProfile()` endpoint returns updated role
- [ ] Check database: did `RoleId` actually change?
- [ ] Check JWT token: does it include role claim?

---

## Testing with Different Origins (ngrok for webhooks)

If testing webhooks with ngrok:

### 1. Start ngrok
```bash
ngrok http 4200
# Output: Forwarding https://xxxx-xxxx.ngrok-free.dev -> http://localhost:4200
```

### 2. Update backend to use ngrok origin
```csharp
var successUrl = "https://xxxx-xxxx.ngrok-free.dev/paymob/response";
var cancelUrl = "https://xxxx-xxxx.ngrok-free.dev/paymob/cancel";
var finalSuccessUrl = $"{successUrl}?session_id={{CHECKOUT_SESSION_ID}}";
```

### 3. Test payment
- Navigate to `https://xxxx-xxxx.ngrok-free.dev/plans`
- Click Subscribe
- Complete payment
- Should redirect to `https://xxxx-xxxx.ngrok-free.dev/paymob/response?session_id=...`

---

## Common Stripe API Errors

### 400: Invalid Request
```json
{"error": {"message": "Invalid string: success_url must be a valid URL"}}
```
**Fix:** Ensure `success_url` is a valid absolute URL (starts with http:// or https://)

### 403: Incorrect API Key Used
```json
{"error": {"message": "Incorrect API Key provided"}}
```
**Fix:** Use test key (sk_test_...) not live key (sk_live_...)

### 404: Session Not Found
```json
{"error": {"message": "No such checkout.session"}}
```
**Fix:** Verify session ID is correct and from the right Stripe account

### 405: Method Not Allowed
```json
{"error": {"message": "You must use POST request"}}
```
**Fix:** Use POST method for create-session and verify

---

## Success Indicators

✅ Backend logs show correct URLs
✅ Stripe Dashboard session shows correct success_url
✅ Browser redirects after payment
✅ Frontend shows success message
✅ Database shows role updated
✅ User can access premium features

---

## Quick Reference: What Should Happen

```
User clicks Subscribe
  ↓
Frontend: POST /api/payment/create-session
  ↓
Backend: Create Stripe session with SuccessUrl = "http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}"
  ↓
Backend returns: checkoutUrl = "https://checkout.stripe.com/pay/cs_test_..."
  ↓
Frontend: window.location.href = checkoutUrl
  ↓
Browser navigates to Stripe Checkout
  ↓
User enters card and clicks Pay
  ↓
Stripe processes payment
  ↓
Stripe redirects to: http://localhost:4200/paymob/response?session_id=cs_test_...
  ↓
Frontend component loads, extracts session_id from URL
  ↓
Frontend: POST /api/payment/verify with { "providerReference": "cs_test_..." }
  ↓
Backend verifies and updates user role
  ↓
Frontend shows success message and redirects to dashboard
  ↓
✓ User sees premium features unlocked
```

If any step fails, check the file/section above.
