// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Debounced search suggestions (Milestone 4.3) - waits for the visitor to
// pause typing before asking the server, rather than firing on every keystroke.
(function () {
    var input = document.getElementById('searchInput');
    var dropdown = document.getElementById('searchSuggestions');
    if (!input || !dropdown) {
        return;
    }

    var debounceTimer = null;
    var currentRequest = null;

    function hideDropdown() {
        dropdown.style.display = 'none';
        dropdown.innerHTML = '';
    }

    function escapeHtml(value) {
        var div = document.createElement('div');
        div.textContent = value == null ? '' : value;
        return div.innerHTML;
    }

    function renderSuggestions(items) {
        if (!items || items.length === 0) {
            hideDropdown();
            return;
        }

        dropdown.innerHTML = items.map(function (item) {
            var image = item.imagePath
                ? '<img src="' + escapeHtml(item.imagePath) + '" alt="" style="width:32px;height:32px;object-fit:cover" class="me-2">'
                : '<span class="me-2" style="display:inline-block;width:32px"></span>';
            var category = item.categoryName ? ' <span class="text-muted small">in ' + escapeHtml(item.categoryName) + '</span>' : '';
            return '<a href="' + escapeHtml(item.link) + '" class="list-group-item list-group-item-action d-flex align-items-center">' +
                image + '<span class="flex-grow-1">' + escapeHtml(item.name) + category + '</span>' +
                '<span class="fw-semibold ms-2">' + item.price.toFixed(2) + '</span></a>';
        }).join('');
        dropdown.style.display = 'block';
    }

    input.addEventListener('input', function () {
        var term = input.value.trim();
        clearTimeout(debounceTimer);

        if (term.length < 2) {
            hideDropdown();
            return;
        }

        debounceTimer = setTimeout(function () {
            if (currentRequest) {
                currentRequest.abort();
            }

            var controller = new AbortController();
            currentRequest = controller;

            fetch('/Search/Suggestions?q=' + encodeURIComponent(term), { signal: controller.signal })
                .then(function (response) { return response.ok ? response.json() : []; })
                .then(renderSuggestions)
                .catch(function () { /* network error or aborted - leave the dropdown as-is */ });
        }, 300);
    });

    document.addEventListener('click', function (event) {
        if (!dropdown.contains(event.target) && event.target !== input) {
            hideDropdown();
        }
    });
}());

// Shared helpers for Cart's AJAX endpoints (Milestone 6.1) - the CSRF token
// travels as a header instead of a form field since these are JSON POSTs, not
// posted <form>s. See _Layout.cshtml's csrf-token meta tag.
function getCsrfToken() {
    var meta = document.querySelector('meta[name="csrf-token"]');
    return meta ? meta.getAttribute('content') : '';
}

function postJson(url, body) {
    return fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'X-Requested-With': 'XMLHttpRequest',
            'X-CSRF-TOKEN': getCsrfToken()
        },
        body: body === undefined ? null : JSON.stringify(body)
    });
}

// Cart page: quantity update, remove, and clear all AJAX-submit then reload
// the page - simpler and just as correct as hand-patching every total/subtotal
// on the page, and this page isn't performance-sensitive enough to need it.
(function () {
    var hasCartItems = document.querySelectorAll('.cart-item').length > 0;
    var clearButton = document.getElementById('cartClearButton');
    var applyCouponButton = document.getElementById('cartApplyCouponButton');
    var removeCouponButton = document.getElementById('cartRemoveCouponButton');
    if (!hasCartItems && !clearButton && !applyCouponButton && !removeCouponButton) {
        return;
    }

    var errorBox = document.getElementById('cartPageError');

    function showError(message) {
        if (errorBox) {
            errorBox.textContent = message;
            errorBox.classList.remove('d-none');
        }
    }

    function handleResponse(response) {
        if (response.ok) {
            window.location.reload();
            return;
        }
        response.json().then(function (problem) {
            showError((problem && problem.detail) || 'Something went wrong. Please try again.');
        }).catch(function () {
            showError('Something went wrong. Please try again.');
        });
    }

    document.querySelectorAll('.cart-quantity-input').forEach(function (input) {
        input.addEventListener('change', function () {
            var quantity = parseInt(input.value, 10);
            if (!quantity || quantity < 1) {
                return;
            }
            postJson('/Cart/UpdateQuantity', { cartItemId: parseInt(input.dataset.cartItemId, 10), quantity: quantity })
                .then(handleResponse);
        });
    });

    document.querySelectorAll('.cart-remove-button').forEach(function (button) {
        button.addEventListener('click', function () {
            postJson('/Cart/Remove', { cartItemId: parseInt(button.dataset.cartItemId, 10) })
                .then(handleResponse);
        });
    });

    if (clearButton) {
        clearButton.addEventListener('click', function () {
            postJson('/Cart/Clear').then(handleResponse);
        });
    }

    if (applyCouponButton) {
        applyCouponButton.addEventListener('click', function () {
            var input = document.getElementById('cartCouponInput');
            var couponCode = input ? input.value.trim() : '';
            if (!couponCode) {
                return;
            }
            postJson('/Cart/ApplyCoupon', { couponCode: couponCode }).then(handleResponse);
        });
    }

    if (removeCouponButton) {
        removeCouponButton.addEventListener('click', function () {
            postJson('/Cart/RemoveCoupon').then(handleResponse);
        });
    }
}());

// Wishlist page: remove-from-wishlist buttons (Milestone 6.3) - AJAX-submit
// then reload, same simple pattern as the cart page's remove/clear.
(function () {
    var removeButtons = document.querySelectorAll('.wishlist-remove-button');
    if (removeButtons.length === 0) {
        return;
    }

    var errorBox = document.getElementById('wishlistPageError');

    removeButtons.forEach(function (button) {
        button.addEventListener('click', function () {
            postJson('/Wishlist/Remove', { productId: parseInt(button.dataset.productId, 10) })
                .then(function (response) {
                    if (response.ok) {
                        window.location.reload();
                        return;
                    }
                    if (errorBox) {
                        errorBox.textContent = 'Something went wrong. Please try again.';
                        errorBox.classList.remove('d-none');
                    }
                });
        });
    });
}());
