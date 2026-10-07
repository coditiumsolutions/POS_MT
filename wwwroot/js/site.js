(function () {
    var sidebar = document.getElementById("posSidebar");
    var backdrop = document.getElementById("posBackdrop");

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
