# Customer User Guide

## What you can do today (after Milestone 4.3)

- **Create an account**: go to *Register*, fill in your name/email/password.
  You're signed in immediately afterward.
- **Log in / out**: use *Log in* in the top navigation; *Log out* signs you
  out of this device.
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
  configured). "Best sellers," "Recommended for you," and "Recently viewed"
  are placeholders for now (they need order history and browsing history
  from later milestones).
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
- Products themselves aren't clickable yet - product detail pages (full
  description, variant selection, reviews) arrive in Milestone 5.

Cart, wishlist, checkout, and order tracking arrive in later milestones
(6 onward).
