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

    window.posDataTable = function (selector) {
        if (!window.jQuery || !jQuery.fn.DataTable) {
            return;
        }

        jQuery(selector).DataTable({
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            order: [],
            autoWidth: false
        });
    };
})();
