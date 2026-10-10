(function () {
    var sidebar = document.getElementById("posSidebar");
    var backdrop = document.getElementById("posBackdrop");
    var toggle = document.getElementById("posSidebarToggle");
    var storageKey = "posSidebarCollapsed";

    function setCollapsed(collapsed) {
        if (!sidebar || !toggle) {
            return;
        }

        sidebar.classList.toggle("collapsed", collapsed);
        toggle.setAttribute("aria-label", collapsed ? "Expand sidebar" : "Collapse sidebar");
        toggle.setAttribute("title", collapsed ? "Expand sidebar" : "Collapse sidebar");
        toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");

        var icon = toggle.querySelector("i");
        if (icon) {
            icon.className = collapsed ? "bi bi-chevron-right" : "bi bi-list";
        }

        try {
            localStorage.setItem(storageKey, collapsed ? "1" : "0");
        } catch (e) {
            /* ignore storage errors */
        }
    }

    function closeSidebar() {
        if (!sidebar || !backdrop) {
            return;
        }

        sidebar.classList.remove("open");
        backdrop.classList.remove("show");
    }

    if (backdrop) {
        backdrop.addEventListener("click", closeSidebar);
    }

    if (sidebar && toggle) {
        var saved = false;
        try {
            saved = localStorage.getItem(storageKey) === "1";
        } catch (e) {
            saved = false;
        }

        setCollapsed(saved);

        toggle.addEventListener("click", function () {
            setCollapsed(!sidebar.classList.contains("collapsed"));
        });
    }

    window.posDataTable = function (selector, options) {
        if (!window.jQuery || !jQuery.fn.DataTable) {
            return;
        }

        options = options || {};
        var lengthBelow = !!options.lengthBelow;
        var hideFilter = !!options.hideFilter;
        delete options.lengthBelow;
        delete options.hideFilter;

        var config = {
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            order: [],
            autoWidth: false
        };

        // Bootstrap5 integration sets a default dom with "f" (search). Override completely when needed.
        if (lengthBelow && hideFilter) {
            config.dom =
                "<'row dt-row'<'col-sm-12'tr>>" +
                "<'row dt-controls-bottom align-items-center mt-2'<'col-sm-12 col-md-4'l><'col-sm-12 col-md-4'i><'col-sm-12 col-md-4'p>>";
        } else if (lengthBelow) {
            config.dom =
                "<'row'<'col-sm-12 col-md-6'f>>" +
                "<'row dt-row'<'col-sm-12'tr>>" +
                "<'row dt-controls-bottom align-items-center mt-2'<'col-sm-12 col-md-4'l><'col-sm-12 col-md-4'i><'col-sm-12 col-md-4'p>>";
        } else if (hideFilter) {
            config.dom =
                "<'row'<'col-sm-12 col-md-6'l>>" +
                "<'row dt-row'<'col-sm-12'tr>>" +
                "<'row'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'p>>";
        }

        jQuery.extend(true, config, options);

        // Ensure Bootstrap default search slot cannot come back.
        if (hideFilter && typeof config.dom === "string") {
            config.dom = config.dom.replace(/f/g, "");
        }

        return jQuery(selector).DataTable(config);
    };

    function closeTopDropdowns(except) {
        document.querySelectorAll("[data-top-dropdown]").forEach(function (dropdown) {
            if (except && dropdown === except) {
                return;
            }
            dropdown.classList.remove("open");
            var menu = dropdown.querySelector(".pos-top-dropdown-menu");
            var twistee = dropdown.querySelector("[data-top-twistee]");
            if (menu) {
                menu.hidden = true;
            }
            if (twistee) {
                twistee.setAttribute("aria-expanded", "false");
            }
        });
    }

    document.querySelectorAll("[data-top-twistee]").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();
            var dropdown = btn.closest("[data-top-dropdown]");
            if (!dropdown) {
                return;
            }
            var willOpen = !dropdown.classList.contains("open");
            closeTopDropdowns(willOpen ? dropdown : null);
            dropdown.classList.toggle("open", willOpen);
            var menu = dropdown.querySelector(".pos-top-dropdown-menu");
            if (menu) {
                menu.hidden = !willOpen;
            }
            btn.setAttribute("aria-expanded", willOpen ? "true" : "false");
        });
    });

    document.addEventListener("click", function () {
        closeTopDropdowns(null);
    });
})();
