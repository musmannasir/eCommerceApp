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
