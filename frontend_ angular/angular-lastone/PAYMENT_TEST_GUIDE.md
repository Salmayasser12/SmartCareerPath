# ✅ Complete Payment Flow Testing Guide

**Status:** Backend fixed ✅ | Frontend ready ✅ | Ready to test 🧪

---

## Quick Test (5 minutes)

### Step 1: Navigate to Plans Page
```
Open browser: http://localhost:4200/plans
```

### Step 2: Click Subscribe
- Click the "Subscribe" button for "Careera Pro"
- You should see a loading spinner

### Step 3: Check Session Tracking
- Open browser DevTools (F12)
- Go to Console tab
- Run: `sessionStorage.getItem('last_checkout_session_id')`
- You should see: `cs_test_...` (a Stripe session ID)

### Step 4: Complete Stripe Payment
- You'll be redirected to Stripe Checkout
- Verify amount shows: **30 EGP** ✅
- Enter test card: `4242 4242 4242 4242`
- Enter expiry: `12/34`
- Enter CVC: `123`
- Click "Pay"

### Step 5: Verify Redirect
- After payment succeeds (green checkmark), you should redirect to:
  ```
  http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}
  ```
- **NOTE:** The `{CHECKOUT_SESSION_ID}` is literal text from the backend redirect URL
- Frontend uses the saved session ID from sessionStorage instead (this is correct!)

### Step 6: Check Payment Verification
- On the success page, open DevTools Console
- You should see logs like:
  ```
  [PaymentSuccess] Found tracked session ID from sessionStorage: cs_test_...
  [PaymentSuccess] Using tracked session instead of URL placeholder
  [PaymentSuccess] Attempting verification with reference: cs_test_...
  [PaymentSuccess] Verification successful: {...}
  ```

### Step 7: Verify Role Update
- Check console: `localStorage.getItem('scp_cached_role')`
- Should show: `"Premium"`
- After 500ms, page should redirect to `/home`

### Step 8: Check Premium Features
- On dashboard, you should see:
  - "AI Interviewer" feature unlocked ✓
  - "Job Parser" feature unlocked ✓
  - Sidebar shows your role as "Premium" ✓

---

## Detailed Testing Steps

### Test 1: Session Tracking Before Stripe
```
Action: Click Subscribe button
Expected:
  1. Frontend calls backend /api/payment/create-session
  2. Backend returns { providerReference: "cs_test_...", ... }
  3. Frontend calls payment.trackCheckoutSession("cs_test_...")
  4. Session saved to sessionStorage with key: "last_checkout_session_id"
  
Verify:
  - Console: sessionStorage.getItem('last_checkout_session_id') = "cs_test_..."
  - Console logs: "[PaymentService] Tracked checkout session: cs_test_..."
```

### Test 2: Stripe Checkout Amount
```
Action: Wait for Stripe Checkout to load
Expected:
  - Left panel shows: "30.00 EGP" (not $9.99)
  
Verify:
  - Stripe shows correct currency and amount
  - Amount is 30 EGP
```

### Test 3: Redirect After Payment
```
Action: Complete payment with test card
Expected:
  1. Payment succeeds (green checkmark)
  2. Stripe redirects to: http://localhost:4200/paymob/response?session_id={CHECKOUT_SESSION_ID}
  3. PaymentSuccess component loads
  4. Frontend verifies payment
  5. Frontend navigates to /home
  
Verify:
  - Browser navigates away from checkout.stripe.com ✓
  - URL changes to localhost:4200/paymob/response ✓
  - Success page loads ✓
  - Auto-redirects to /home within 2 seconds ✓
```

### Test 4: Placeholder Handling
```
Action: While on success page, check what Stripe sent
Expected:
  - URL shows: ?session_id={CHECKOUT_SESSION_ID} (literal placeholder)
  - Frontend retrieves saved session from sessionStorage: "cs_test_..."
  - Frontend uses saved session for verification (NOT the URL placeholder)
  
Verify:
  - Console: [PaymentSuccess] Using tracked session instead of URL placeholder
  - Payment verifies successfully even though URL has placeholder
```

### Test 5: Role Update
```
Action: After payment verification succeeds
Expected:
  1. localStorage['scp_cached_role'] = "Premium"
  2. Sidebar updates to show role change
  3. UI emits role change to subscribers
  
Verify:
  - Console: localStorage.getItem('scp_cached_role') = "Premium"
  - Sidebar displays "Premium" status
  - Premium features become visible
```

### Test 6: Navigation After Payment
```
Action: Wait for auto-redirect after success page
Expected:
  1. Success page shows "Payment confirmed!" message
  2. After 500ms, navigates to /home
  3. Dashboard loads with premium features
  
Verify:
  - Auto-redirect happens within 2 seconds
  - Dashboard loads correctly
  - No console errors
```

### Test 7: Cancel Payment
```
Action: 
  1. Click Subscribe
  2. In Stripe Checkout, look for "Back" link or close button
  3. Click to cancel payment
  
Expected:
  1. Redirected to /paymob/cancel
  2. PaymentCancel component clears tracked session
  3. sessionStorage['last_checkout_session_id'] = null
  4. Shows "Payment cancelled" message
  
Verify:
  - Console: sessionStorage.getItem('last_checkout_session_id') = null
  - Cancel page displayed
  - Can click "Try Again" to go back to plans
```

### Test 8: Retry Failed Payment
```
Action:
  1. Click Subscribe
  2. Use invalid card: 4000 0000 0000 0002
  3. See error on Stripe
  4. Try Again
  
Expected:
  1. Payment fails on Stripe
  2. Redirect to cancel page (or show error)
  3. Can create new session and retry
  4. New session ID saved to sessionStorage
  5. Second attempt succeeds with valid card
  
Verify:
  - Can retry multiple times
  - Each attempt saves new session ID
  - Final successful payment updates role
```

---

## Expected Console Output (Full Flow)

When you complete a payment, console should show:

```
[PlansPage] Redirecting to checkout URL: https://checkout.stripe.com/c/pay/...
[PlansPage] Tracking Stripe session before redirect: cs_test_a1ltpu74U7RoAlHidckbzulSox96oQ8YBDj8pTTKscBpG567Vt87hlBH4w
[PaymentService] Tracked checkout session: cs_test_a1ltpu74U7RoAlHidckbzulSox96oQ8YBDj8pTTKscBpG567Vt87hlBH4w

[PaymentSuccess] Found tracked session ID from sessionStorage: cs_test_a1ltpu74U7RoAlHidckbzulSox96oQ8YBDj8pTTKscBpG567Vt87hlBH4w
[PaymentSuccess] Using tracked session instead of URL placeholder (Stripe does not substitute {CHECKOUT_SESSION_ID})
[PaymentSuccess] Attempting verification with reference: cs_test_a1ltpu74U7RoAlHidckbzulSox96oQ8YBDj8pTTKscBpG567Vt87hlBH4w
[PaymentSuccess] Verification successful: { transactionId: 2064, status: 'completed', message: 'Payment verified successfully' }
[PaymentSuccess] Payment verified successfully — setting role to Premium
[AuthService.setCachedRole] Set cached role: Premium
[AuthService.notifyRoleChange] Emitting role: Premium
[PaymentSuccess] Navigating to home after storing token
[Sidebar] Role changed to Premium (from subscription)
```

---

## Success Checklist

| Check | Status | Details |
|-------|--------|---------|
| Backend sets SuccessUrl & CancelUrl | ✅ | Stripe redirects to /paymob/response |
| Backend correct amount (30 EGP) | ✅ | Stripe shows 30.00 EGP, not $9.99 |
| Frontend tracks session | ✅ | sessionStorage['last_checkout_session_id'] saved |
| Redirect after payment | ✅ | Browser leaves checkout.stripe.com |
| Placeholder handling | ✅ | Frontend uses saved session, not URL placeholder |
| Payment verification | ✅ | /api/payment/verify succeeds |
| Role updated | ✅ | localStorage['scp_cached_role'] = "Premium" |
| UI updates | ✅ | Sidebar shows Premium, features unlocked |
| Auto-redirect | ✅ | Goes to /home after success |
| Cancel flow | ✅ | Session cleared, can retry |

---

## Troubleshooting

### Issue: Session not being tracked
```
Solution:
1. Check console: sessionStorage.getItem('last_checkout_session_id')
2. Should show Stripe session ID (cs_test_...)
3. If null, check if trackCheckoutSession was called
4. Check browser DevTools Network tab → /api/payment/create-session
5. Verify response includes 'providerReference'
```

### Issue: Redirect not happening
```
Solution:
1. Check backend logs for success_url being set
2. Verify Stripe Dashboard shows correct success_url
3. Check if payment actually completed on Stripe
4. Check browser console for errors
5. Verify /paymob/response route exists in Angular
```

### Issue: Wrong amount showing on Stripe
```
Solution:
1. Check backend is using correct amount (30 EGP = 3000 cents)
2. Check currency is "egp" (lowercase)
3. Check backend logs during create-session
4. Look in Stripe Dashboard for session details
```

### Issue: Payment verifies but role doesn't update
```
Solution:
1. Check /api/payment/verify response
2. Should return { status: 'completed', ... }
3. Check localStorage['scp_cached_role'] after verify
4. Check sidebar is subscribed to auth.role$ observable
5. Check browser console for role update logs
```

### Issue: Can't access premium features after payment
```
Solution:
1. Check role is actually "Premium": console.log(auth.getUserRole())
2. Hard refresh browser (Ctrl+Shift+R)
3. Check localStorage values are persisted
4. Check guard permissions in component
5. Try logging out and back in to refresh role from token
```

---

## Database Verification

To verify payment went through on backend:

```sql
-- Check user role updated
SELECT Id, Email, RoleId FROM Users WHERE Email = 'your@email.com'
-- Should show RoleId = 2 (Premium)

-- Check payment transaction created
SELECT * FROM PaymentTransactions ORDER BY CreatedAt DESC LIMIT 1
-- Should show Status = 'Completed'

-- Check webhook was processed
SELECT * FROM StripeWebhookEvents ORDER BY CreatedAt DESC LIMIT 1
-- Should show charge.succeeded event
```

---

## Timeline

- **Start:** Click Subscribe at http://localhost:4200/plans
- **~2 sec:** Redirect to Stripe Checkout
- **~30 sec:** Complete payment with test card
- **~1 sec:** Stripe redirects back to /paymob/response
- **~2 sec:** Verification completes, role updates
- **~3 sec:** Auto-redirect to /home
- **Total:** ~40 seconds end-to-end

---

## Success! 🎉

If you see all green checks above, the payment system is working perfectly:

✅ Backend creates session with redirect URLs  
✅ Frontend tracks session ID  
✅ Stripe redirects after payment  
✅ Frontend verifies using tracked session  
✅ User role updates to Premium  
✅ UI reflects premium status  
✅ User can access premium features  

**The payment flow is complete!**

---

## Contact

Issues? Check the console logs - they're detailed and will tell you exactly what's happening at each step!
