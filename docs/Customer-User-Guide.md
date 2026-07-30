# Customer User Guide

## What you can do today (after Milestone 7.3)

- **Create an account**: go to *Register*, fill in your name/email/password.
  You're signed in immediately afterward. If you had anything in your cart
  as a guest, it comes with you.
- **Log in / out**: use *Log in* in the top navigation; *Log out* signs you
  out of this device. Logging in also folds in anything you added to your
  cart as a guest, combining it with whatever's already in your account's
  cart.
- **Forgot your password?**: use the link on the login page. If an account
  exists for that email, you'll get a reset link (in development, this is
  written to a local preview file instead of a real email - see the README).
- **Change your password**: from your profile page, once logged in. This
  signs out any other devices you were logged in on.
- **View your profile**: name, email, roles, member-since date, last login.
- **Revoke all sessions**: from your profile, if you think your account may
  be compromised - this signs you out everywhere, including this device.

### Browsing the store

- **Home page**: hero banners, featured categories/products, new arrivals,
  and discounted products (all admin-managed or drawn from real catalog
  data - see `Admin-User-Guide.md`'s Marketing section for how banners are
  configured), plus a **Recently viewed** section once you've looked at a
  product. "Best sellers" and "Recommended for you" are still placeholders
  for now - they need order history from a later milestone, or (for a
  home-page recommendation) a signal the store doesn't have yet.
- **Category pages** (linked from the header nav or a featured-category
  card): shows every product in that category, including its subcategories.
- **Brand pages**: linked from any product card's brand name, or from the
  full **Brands** page in the header nav.
- **Search**: the header search box searches product name, SKU, brand,
  category, tags, keywords, and short description, with the closest matches
  (name starting with your search term) ranked first. Start typing and a
  dropdown of quick suggestions (image, price, category) appears after a
  short pause - click one to jump straight to it.
- **Filters**: every listing page (all products, a category, a brand, or
  search results) has a filter panel - price range, category/subcategory,
  brand, product attributes (e.g. Color, Size), in-stock only, discounted,
  featured, and new arrivals. Filters combine (e.g. "Featured AND under $50
  AND Blue") and stay in the page's URL, so a filtered link can be bookmarked
  or shared. A "Clear filters" link resets them.
- **Sorting**: relevance (search results), newest, price (low-to-high or
  high-to-low), biggest discount, or name (A-Z or Z-A). Sorting by rating or
  "best selling" isn't available yet - the store doesn't have reviews or
  order history to sort by until later milestones.
- **All of the above**: switch between grid and list view, page through
  results, and clear the current category/brand/search filter with the "×"
  next to its label if you want to go back to browsing everything.

### Viewing a product

Click any product card to open its detail page:

- **Gallery**: click the main image to zoom in; click a thumbnail to swap
  the main image.
- **Price**: selling price, a struck-through compare-at price and discount
  percentage when the product is on sale, and a note on whether tax applies.
- **Stock**: In Stock, Low Stock (with how many are left), Available on
  Backorder, or Out of Stock.
- **Options** (Color, Size, etc., if the product has them): pick a value for
  each and the page updates instantly - no reload - to that exact variant's
  SKU, price, and stock. Options that can't be combined with what you've
  already picked (because no such variant exists) are automatically greyed
  out, so you can't select an invalid combination in the first place. If you
  land on the page via a link/bookmark for a combination that isn't
  available, you'll see a note and the closest match instead.
- **Quantity and Add to Cart**: pick a quantity and click Add to Cart - it's
  added right away without leaving the page, and the Cart icon in the header
  updates to show how many items you have.
- **Add to Wishlist**: click to save the product for later - the button
  fills in to show it's saved, and clicking it again removes it. This
  requires an account (unlike Cart, which works for guests too); if you're
  not signed in, it takes you to the login page first.
- **Description, Specifications, Warranty/Returns/Shipping, Reviews** tabs.
  Reviews aren't built yet (Milestone 12), so that tab says so honestly
  rather than showing fake ratings.
- **Related products**: other products picked for you based on category,
  brand, shared tags, and similar price - not just "same category" anymore.
- **Recently viewed**: products you've looked at, most recent first, shown
  further down the page and on the home page. Signed out, this is remembered
  in a cookie on your device for 90 days; signed in, it's tied to your
  account instead, so it follows you between devices.
- "Frequently bought together" is still a placeholder - it needs real order
  history (Milestone 9) that doesn't exist yet.

### Your Cart

Click **Cart** in the header (it shows a badge with your item count once you
have anything in it) to see everything you've added:

- Each line shows the product, its selected option (if any), price, and a
  quantity you can change - just edit the number and it updates right away.
- **Remove** takes an item out entirely; **Clear cart** empties it.
- If you try to set a quantity higher than what's in stock, you'll see a
  message telling you how many are actually available.
- If something you added is no longer available (discontinued, out of
  stock with no backorder, etc.), it still shows in your cart so you know
  what happened, but it's excluded from your subtotal and can only be
  removed, not adjusted.
- If a price changes after you've added something (a sale starts or ends,
  for instance), you'll see a note showing the old and new price - you're
  always charged the current price, never a stale one.
- If stock runs low after you've added something (someone else bought the
  last few), you'll see a note telling you how many are actually available
  now, so you can adjust the quantity yourself before it becomes a problem
  at checkout.
- You don't need an account to use the cart - as a guest, it's remembered
  on your device for 30 days. If you log in, your cart follows your account
  instead, the same way recently-viewed does - and if you already had items
  in your account's cart from a previous visit, anything from your guest
  session is added alongside them rather than replacing them.
- **Coupon code**: enter one in the box under your cart items and click
  Apply - if it's valid, you'll see the discount and a new Total
  (Subtotal minus the discount) right away, plus the promotion's name next
  to the code. Click **Remove** to take it off. Only one coupon can be
  applied at a time - applying a new one replaces whichever was there
  before. If a code doesn't work (wrong code, expired, minimum order not
  met, or your cart doesn't have anything it applies to), you'll see a
  message explaining why, and your cart itself isn't changed.
- **Estimated tax**: if the store has tax set up for its default region,
  you'll see an "Estimated tax" line under your Total. This is an
  estimate, not your final tax - it's calculated at checkout once your
  actual shipping destination is known.
- **Estimated shipping**: if the store has a shipping method set up, you'll
  also see an "Estimated shipping" line (or "Free" if your order qualifies
  for free shipping). Like estimated tax, this is a preview based on the
  store's typical shipping option - your actual shipping cost and choice
  of delivery speed are confirmed at checkout. If you have a coupon applied,
  both the estimated tax and estimated shipping (including whether you
  qualify for free shipping) are calculated after your discount is applied,
  not before.
- **Estimated total**: when tax and/or shipping estimates are shown, you'll
  also see an "Estimated total" line - your Total plus estimated tax and
  shipping, all in one number. Still just a preview, same as its parts.
- **Checkout** isn't available yet - that arrives in a later milestone.

### Your Wishlist

Click **Wishlist** in the header (it shows a badge with your item count) to
see everything you've saved. This requires an account - it's not available
as a guest, since a wishlist is meant to follow you across visits and
devices, not just sit in a cookie on one browser.

- Each saved product shows its card, just like elsewhere on the site.
- **Remove** takes it off your wishlist.
- Toggle it on/off directly from any product's detail page too - no need to
  come back here to manage it.
- If something you saved is no longer available (discontinued, etc.), it
  just quietly disappears from the list rather than cluttering it with
  something you can't act on.

### Your addresses

Go to your **Profile** page and click **Manage addresses** to see your saved
addresses. Requires an account, same as Wishlist.

- **New address** opens a form for name, phone, address lines, city,
  region/state, postal code, and country.
- Your very first saved address is automatically your default - there's
  nothing to compare it against yet.
- **Set as default** switches which address is used as the default; only one
  address can be the default at a time.
- **Edit** updates any field, including which address is the default.
- **Delete** removes an address for good. If you delete your default
  address, you're left with no default at all until you set a new one -
  nothing is picked for you automatically.

### Checking out

Click **Checkout** on your Cart page (requires an account, same as
Wishlist and your address book - if you're a guest, you'll be asked to log
in first, and anything already in your cart comes with you).

1. **Choose a shipping address** from your saved addresses (your default is
   pre-selected). If you haven't saved one yet, you'll be asked to add one
   first, then brought straight back here.
2. **Choose a shipping method** - every option available for that address,
   with its real cost (already reflecting any coupon you've applied).
3. **Review your order** - the real subtotal, discount, tax, and shipping
   for your chosen address, plus the grand total. This replaces the
   Cart page's estimates with your actual numbers. You'll also enter a
   card number, cardholder name, expiry, and security code here - this is a
   **simulated payment gateway**, so no real charge ever occurs; the page
   tells you which test card number simulates a successful charge and
   which one simulates a decline.
4. **Place order** - re-checks everything one last time (your cart, stock,
   address, and shipping method) before confirming, since a few seconds can
   pass between reviewing and clicking. It then reserves the stock for each
   item in your order before charging anything - if an item you ordered no
   longer has enough stock available, your card is never charged. If
   something in your cart no longer has enough stock at all, you're sent
   back to your Cart page to fix it instead of a confusing failure. If you
   click Place order more than once (or your browser retries the request),
   you won't get charged twice or see a different result the second time -
   you'll land on the same order either way.

You'll then see your order confirmation, one of three ways. If stock was
reserved and the (simulated) charge succeeded, you'll see a real order
number, your payment method, and your cart emptied. If the stock for
something you ordered couldn't be secured at the last moment, you'll still
see a real order number, but your card was never charged and the order is
marked accordingly, naming what ran out - adjust the quantity in your cart
and check out again. If your card was declined, you'll also see a real
order number - the order was placed - but marked as payment failed, with
the reason why; your cart is left exactly as it was so you can go back and
check out again with a different card. **A confirmation email arrives in a
later milestone.**
