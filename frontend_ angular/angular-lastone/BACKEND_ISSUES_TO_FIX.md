# Backend Issues Found - Immediate Action Required

## Issue #1: Stripe Redirect Not Working ❌

### Symptom
- User completes payment on Stripe ✓
- Payment succeeds (green checkmark) ✓
- **Database role updates** (webhook works) ✓
- **Browser doesn't redirect back to frontend** ❌
- User stuck on checkout.stripe.com

### Root Cause
Backend's `CreateSession` method is NOT setting `SuccessUrl` and `CancelUrl` in the Stripe SessionCreateOptions.

### Solution
In your C# backend `CreateSession` method, add these 2 lines when creating `SessionCreateOptions`:

```csharp
var sessionOptions = new SessionCreateOptions
{
    Mode = "subscription", // or "payment" for one-time
    
    // ADD THESE TWO LINES:
    SuccessUrl = $"{request.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
    CancelUrl = request.CancelUrl,
    
    // ... rest of options
};
```

**Critical:** The `{{CHECKOUT_SESSION_ID}}` placeholder (with double braces) will be automatically replaced by Stripe with the actual session ID.

### Testing
1. Create a new Stripe session via `/api/payment/create-session`
2. Verify the session in Stripe Dashboard: Settings → Webhooks → Event → View session JSON
3. Look for `success_url` and `cancel_url` fields
4. They should contain: `http://localhost:4200/paymob/response?session_id=...`

---

## Issue #2: Wrong Price on Stripe Checkout ❌

### Symptom
- Frontend shows: **30 EGP** ✓
- Backend pricing endpoint returns: **30 EGP** ✓
- **Stripe Checkout displays: $9.99** ❌

### Root Cause
Backend is either:
1. Using hardcoded prices instead of database/request values
2. Using wrong currency conversion
3. Creating line items with incorrect amount

### Solution
In your `CreateSession` method, verify:

```csharp
// Get the correct amount from database or request
decimal amount = 30m; // Should be 30 EGP (3000 cents)

// Convert to cents for Stripe (multiply by 100)
long amountInCents = (long)(amount * 100); // Should be 3000

var lineItems = new List<SessionLineItemOptions>
{
    new SessionLineItemOptions
    {
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = "egp", // Must be lowercase!
            ProductData = new SessionLineItemPriceDataProductDataOptions
            {
                Name = "Careera Premium",
                Description = "Monthly subscription"
            },
            UnitAmount = amountInCents, // 3000 for 30 EGP
            Recurring = new SessionLineItemPriceDataRecurringOptions
            {
                Interval = "month",
                IntervalCount = 1
            }
        },
        Quantity = 1
    }
};
```

### Key Points
- ✅ Amount: `30 EGP` = `3000 cents` (amount × 100)
- ✅ Currency: `"egp"` (lowercase, for Egyptian Pound)
- ✅ Use actual values from request/database, NOT hardcoded
- ✅ For monthly: `Interval = "month"`, `IntervalCount = 1`
- ✅ For yearly: `Interval = "month"`, `IntervalCount = 12`

### Testing
1. Check your backend logs - log the `amountInCents` value before sending to Stripe
2. Verify Stripe receives: `amount: 3000` (for 30 EGP)
3. Verify currency: `currency: "egp"`

---

## Frontend Status

✅ **Frontend is 100% ready:**
- Session tracking implemented ✓
- Redirect handling implemented ✓
- Payment verification implemented ✓
- Role updates implemented ✓
- All edge cases handled ✓

**Frontend is waiting for backend to fix these 2 issues.**

---

## Summary - What Backend Needs To Do

| Issue | Fix | Line of Code | Priority |
|-------|-----|--------------|----------|
| **No redirect** | Add `SuccessUrl` and `CancelUrl` to SessionCreateOptions | 2 lines | 🔴 CRITICAL |
| **Wrong price** | Verify amount calculation (should be 3000 cents for 30 EGP) | Check line items | 🔴 CRITICAL |

---

## Quick Checklist

Before deploying, verify:

- [ ] `SuccessUrl = "{request.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}"`
- [ ] `CancelUrl = request.CancelUrl`
- [ ] Amount in cents: `(amount * 100)` = `3000` for `30 EGP`
- [ ] Currency: `"egp"` (lowercase)
- [ ] Stripe Dashboard shows correct `success_url` in session JSON
- [ ] Stripe Checkout displays correct amount

---

## Expected Result After Fix

```
User clicks "Subscribe"
    ↓
Frontend sends 30 EGP to backend ✓
    ↓
Backend creates Stripe session with:
  - Amount: 3000 cents (30 EGP) ✓
  - SuccessUrl: http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID} ✓
  - CancelUrl: http://localhost:4200/paymob/cancel ✓
    ↓
Frontend redirects to Stripe Checkout
    ↓
Stripe Checkout displays: "30.00 EGP" ✓
    ↓
User completes payment
    ↓
User redirects to: http://localhost:4200/paymob/response?session_id=cs_test_...
    ↓
Frontend shows "Payment confirmed!" ✓
    ↓
Frontend navigates to dashboard
    ↓
User sees premium features unlocked ✓
```

---

## Contact

If backend team needs help:
1. Check logs: `dotnet run` output
2. Verify Stripe Dashboard: https://dashboard.stripe.com
3. Use GitHub Copilot with the prompt in `BACKEND_STRIPE_FIX_PROMPT.md`
